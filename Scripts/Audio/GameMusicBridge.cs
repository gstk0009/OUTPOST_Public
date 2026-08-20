using UnityEngine;

public class GameMusicBridge : MonoBehaviour
{
    public MusicManager musicManager;
    public GameCycleDirector cycleDirector;
    public PlayerHealth playerHealth;
    [Range(0f, 1f)] public float criticalHealthRatio = 0.3f;

    private GameCycleDirector.CyclePhase _lastPhase;
    private int _lastCycle;
    private bool _initialized;

    void Start()
    {
        if (musicManager == null) musicManager = FindFirstObjectByType<MusicManager>();
        if (cycleDirector == null) cycleDirector = FindFirstObjectByType<GameCycleDirector>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        RefreshMusic(true);
    }

    void Update()
    {
        if (musicManager == null || cycleDirector == null)
            return;

        if (!_initialized || _lastPhase != cycleDirector.Phase || _lastCycle != cycleDirector.CycleNumber)
            RefreshMusic(false);
        else if (cycleDirector.Phase == GameCycleDirector.CyclePhase.Defending)
            musicManager.SetMusicState(ResolveCombatState());
    }

    void RefreshMusic(bool immediate)
    {
        if (musicManager == null || cycleDirector == null)
            return;

        GameCycleDirector.CyclePhase previousPhase = _lastPhase;
        _lastPhase = cycleDirector.Phase;
        _lastCycle = cycleDirector.CycleNumber;
        bool phaseChanged = !_initialized || previousPhase != _lastPhase;
        _initialized = true;

        switch (_lastPhase)
        {
            case GameCycleDirector.CyclePhase.Preparing:
            case GameCycleDirector.CyclePhase.GeneratorActivation:
            case GameCycleDirector.CyclePhase.Cleared:
            case GameCycleDirector.CyclePhase.HelipadTransit:
                musicManager.SetMusicState(MusicState.Exploration, immediate);
                break;
            case GameCycleDirector.CyclePhase.Defending:
                musicManager.SetMusicState(ResolveCombatState(), immediate);
                if (phaseChanged) musicManager.PlayWaveStartStinger();
                break;
            case GameCycleDirector.CyclePhase.Failed:
                musicManager.SetMusicState(MusicState.Defeat, immediate);
                if (phaseChanged) musicManager.PlayDefeatStinger();
                break;
            case GameCycleDirector.CyclePhase.Victory:
                musicManager.SetMusicState(MusicState.Victory, immediate);
                if (phaseChanged) musicManager.PlayVictoryStinger();
                break;
            case GameCycleDirector.CyclePhase.Extraction:
                musicManager.SetMusicState(MusicState.Victory, immediate);
                if (phaseChanged) musicManager.PlayVictoryStinger();
                break;
        }
    }

    MusicState ResolveCombatState()
    {
        if (cycleDirector.CycleNumber >= 4)
            return MusicState.FinalWave;

        if (playerHealth != null && playerHealth.maxHealth > 0f &&
            playerHealth.CurrentHealth / playerHealth.maxHealth <= criticalHealthRatio)
            return MusicState.CombatHigh;

        if (cycleDirector.CycleNumber <= 1) return MusicState.CombatLow;
        if (cycleDirector.CycleNumber == 2) return MusicState.CombatMedium;
        return MusicState.CombatHigh;
    }
}
