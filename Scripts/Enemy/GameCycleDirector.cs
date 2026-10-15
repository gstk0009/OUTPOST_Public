using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameCycleDirector : MonoBehaviour
{
    public enum CyclePhase { Preparing, Defending, GeneratorActivation, Cleared, HelipadTransit, Extraction, Victory, Failed }

    public GateWaveSpawner waveSpawner;
    public WaveCampaignSO campaign;
    public PlayerHealth playerHealth;
    public DefendObjective defendObjective;
    public HelicopterExtractionController extractionController;
    public ObjectiveBeacon objectiveBeacon;
    public float preparationDuration = 8f;
    public float intermissionDuration = 6f;
    public int baseEnemyCount = 12;
    public int enemiesAddedPerCycle = 8;
    public int totalCycles = 4;

    public int CycleNumber { get; private set; }
    public CyclePhase Phase { get; private set; }
    public float PhaseTimeRemaining { get; private set; }
    public WaveDefinition CurrentWaveDefinition => campaign != null ? campaign.GetWave(CycleNumber) : null;

    private CancellationTokenSource _flowCts;
    private bool _generatorActivated;
    public bool CanActivateGenerator => Phase == CyclePhase.GeneratorActivation && !_generatorActivated;

    void Awake()
    {
        if (waveSpawner == null) waveSpawner = FindFirstObjectByType<GateWaveSpawner>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (defendObjective == null) defendObjective = FindFirstObjectByType<DefendObjective>();
        if (extractionController == null) extractionController = FindFirstObjectByType<HelicopterExtractionController>(FindObjectsInactive.Include);
        if (objectiveBeacon == null) objectiveBeacon = FindFirstObjectByType<ObjectiveBeacon>(FindObjectsInactive.Include);
        if (campaign != null && campaign.waves != null && campaign.waves.Count > 0)
        {
            totalCycles = campaign.waves.Count;
            preparationDuration = campaign.preparationDuration;
            intermissionDuration = campaign.intermissionDuration;
        }
    }

    void Start()
    {
        if (waveSpawner == null)
        {
            enabled = false;
            return;
        }

        GameSessionState.BeginRun();
        defendObjective?.SetOperational(false);
        if (objectiveBeacon != null) objectiveBeacon.gameObject.SetActive(false);
        waveSpawner.autoStart = false;
        waveSpawner.WaveCompleted += OnWaveCompleted;
        StartFlow(PrepareNextCycleAsync);
    }

    void Update()
    {
        if (Phase is CyclePhase.Failed or CyclePhase.Victory) return;
        if (playerHealth != null && playerHealth.IsDead)
            FailShift("PLAYER LOST");
        else if (defendObjective != null && defendObjective.IsDestroyed)
            FailShift("GENERATOR DESTROYED");
    }

    void StartFlow(System.Func<CancellationToken, UniTask> flow)
    {
        _flowCts?.Cancel();
        _flowCts?.Dispose();
        _flowCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        flow(_flowCts.Token).Forget();
    }

    async UniTask PrepareNextCycleAsync(CancellationToken token)
    {
        CycleNumber++;
        GameSessionState.RegisterWave(CycleNumber);
        if (CycleNumber == totalCycles && extractionController != null && extractionController.landingPoint != null)
        {
            defendObjective?.SetOperational(false);
            if (objectiveBeacon != null) objectiveBeacon.gameObject.SetActive(false);
            Phase = CyclePhase.HelipadTransit;
            PhaseTimeRemaining = 0f;
            while (!extractionController.IsPlayerAtLandingZone(playerHealth))
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (token.IsCancellationRequested || Phase == CyclePhase.Failed) return;
            }
        }

        Phase = CyclePhase.Preparing;
        float countdown = CycleNumber == totalCycles ? Mathf.Min(3f, preparationDuration) : preparationDuration;
        await CountdownAsync(countdown, token);
        if (token.IsCancellationRequested || Phase == CyclePhase.Failed) return;

        WaveDefinition definition = CurrentWaveDefinition;
        if (definition != null)
            waveSpawner.ConfigureWave(definition, CycleNumber, campaign);
        else
        {
            waveSpawner.enemyCount = baseEnemyCount + (CycleNumber - 1) * enemiesAddedPerCycle;
            waveSpawner.ConfigureDifficulty(CycleNumber);
        }
        Phase = CyclePhase.Defending;
        defendObjective?.SetOperational(CycleNumber is 2 or 3);
        if (objectiveBeacon != null) objectiveBeacon.gameObject.SetActive(CycleNumber is 2 or 3);
        PhaseTimeRemaining = 0f;
        waveSpawner.StartWave();
    }

    void OnWaveCompleted()
    {
        if (Phase == CyclePhase.Defending)
            StartFlow(ClearIntermissionAsync);
    }

    async UniTask ClearIntermissionAsync(CancellationToken token)
    {
        if (CycleNumber == 1 && !_generatorActivated)
        {
            Phase = CyclePhase.GeneratorActivation;
            PhaseTimeRemaining = 0f;
            return;
        }
        Phase = CyclePhase.Cleared;
        await CountdownAsync(intermissionDuration, token);
        if (token.IsCancellationRequested || Phase == CyclePhase.Failed) return;

        if (CycleNumber >= totalCycles)
            StartFlow(ExtractionAsync);
        else
            StartFlow(PrepareNextCycleAsync);
    }

    public void ActivateGenerator()
    {
        if (!CanActivateGenerator) return;
        _generatorActivated = true;
        defendObjective?.SetOperational(true);
        if (objectiveBeacon != null) objectiveBeacon.gameObject.SetActive(true);
        StartFlow(GeneratorStartupAsync);
    }

    async UniTask GeneratorStartupAsync(CancellationToken token)
    {
        Phase = CyclePhase.Cleared;
        await CountdownAsync(3f, token);
        if (token.IsCancellationRequested || Phase == CyclePhase.Failed) return;
        StartFlow(PrepareNextCycleAsync);
    }

    async UniTask ExtractionAsync(CancellationToken token)
    {
        Phase = CyclePhase.Extraction;
        PhaseTimeRemaining = 0f;
        if (extractionController == null)
        {
            FailShift("HELICOPTER EXTRACTION UNAVAILABLE");
            return;
        }

        bool completed = await extractionController.PlayExtractionAsync(playerHealth, token);
        if (!completed || token.IsCancellationRequested) return;
        Phase = CyclePhase.Victory;
        GameSessionState.Finish(true, "HELICOPTER EXTRACTION");
    }

    async UniTask CountdownAsync(float duration, CancellationToken token)
    {
        PhaseTimeRemaining = duration;
        while (PhaseTimeRemaining > 0f)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            PhaseTimeRemaining = Mathf.Max(0f, PhaseTimeRemaining - Time.deltaTime);
        }
    }

    void FailShift(string reason)
    {
        Phase = CyclePhase.Failed;
        PhaseTimeRemaining = 0f;
        _flowCts?.Cancel();
        waveSpawner?.CancelWave();
        GameSessionState.Finish(false, reason);
    }

    void OnDestroy()
    {
        if (waveSpawner != null) waveSpawner.WaveCompleted -= OnWaveCompleted;
        _flowCts?.Cancel();
        _flowCts?.Dispose();
    }
}
