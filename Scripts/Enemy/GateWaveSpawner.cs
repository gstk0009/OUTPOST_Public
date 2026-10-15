using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

public class GateWaveSpawner : MonoBehaviour
{
    public GameObject walkerPrefab;
    [Tooltip("Basic, Ribcage, Chubby, Arm 순서의 전투 프리팹")]
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints;
    public int enemyCount = 6;
    public float initialDelay = 2f;
    public float spawnInterval = 0.85f;
    public bool autoStart = true;
    public int prewarmCount = 40;
    public float minimumSpawnDistance = 18f;
    public float preferredSpawnDistance = 24f;
    public float maximumSpawnDistance = 38f;
    public float hiddenSpawnRetryDelay = 0.25f;
    public float hiddenSpawnMaxWait = 2f;
    public int AliveCount => _alive.Count;
    public int RemainingCount => _alive.Count + Mathf.Max(0, enemyCount - _spawnedCount);
    public bool IsRunning => _started;
    public bool BossActive { get; private set; }
    public event Action WaveStarted;
    public event Action WaveCompleted;
    public event Action<string> WaveFailed;
    public event Action<Vector3, float> EnemyKilled;

    private readonly List<EnemyHealth> _alive = new();
    private readonly List<Queue<EnemyHealth>> _pools = new();
    private readonly List<EnemyHealth> _all = new();
    private readonly Dictionary<EnemyHealth, int> _poolIndex = new();
    private GameObject[] _sources;
    private bool _started;
    private bool _spawnFinished;
    private int _spawnedCount;
    private int _difficultyWave = 1;
    private WaveDefinition _waveDefinition;
    private int _spawnPointCursor;
    private CancellationTokenSource _spawnCts;

    void Awake()
    {
        _sources = enemyPrefabs != null
            ? Array.FindAll(enemyPrefabs, prefab => prefab != null)
            : Array.Empty<GameObject>();
        if (_sources.Length == 0 && walkerPrefab != null) _sources = new[] { walkerPrefab };
        if (_sources.Length == 0) return;
        for (int i = 0; i < _sources.Length; i++) _pools.Add(new Queue<EnemyHealth>());
        for (int i = 0; i < prewarmCount; i++)
        {
            int type = GetPrewarmType(i);
            _pools[type].Enqueue(CreatePooledEnemy(type));
        }
    }

    void Start()
    {
        if (autoStart) StartWave();
    }

    public void ConfigureDifficulty(int wave) => _difficultyWave = Mathf.Max(1, wave);

    public void ConfigureWave(WaveDefinition definition, int waveNumber, WaveCampaignSO campaign)
    {
        _waveDefinition = definition;
        _difficultyWave = Mathf.Max(1, waveNumber);
        if (definition != null)
        {
            enemyCount = Mathf.Max(1, definition.enemyCount);
            initialDelay = Mathf.Max(0f, definition.initialDelay);
            spawnInterval = Mathf.Max(0.05f, definition.spawnInterval);
            minimumSpawnDistance = Mathf.Max(0f, definition.minimumSpawnDistance);
            preferredSpawnDistance = Mathf.Max(minimumSpawnDistance, definition.preferredSpawnDistance);
            maximumSpawnDistance = Mathf.Max(preferredSpawnDistance, definition.maximumSpawnDistance);
        }
        else if (campaign != null)
        {
            minimumSpawnDistance = campaign.minimumSpawnDistance;
        }
        if (campaign != null)
        {
            hiddenSpawnRetryDelay = campaign.hiddenSpawnRetryDelay;
            hiddenSpawnMaxWait = campaign.hiddenSpawnMaxWait;
        }
    }

    public void StartWave()
    {
        if (_started) return;
        if (_sources == null || _sources.Length == 0 || spawnPoints == null || spawnPoints.Length == 0)
        {
            FailWave("SPAWN CONFIGURATION MISSING");
            return;
        }
        _started = true;
        _spawnFinished = false;
        _spawnedCount = 0;
        _alive.RemoveAll(health => health == null || health.IsDead);
        WaveStarted?.Invoke();
        _spawnCts?.Cancel();
        _spawnCts?.Dispose();
        _spawnCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        SpawnWaveAsync(_spawnCts.Token).Forget();
    }

    async UniTaskVoid SpawnWaveAsync(CancellationToken token)
    {
        bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(initialDelay), cancellationToken: token).SuppressCancellationThrow();
        if (cancelled) return;
        for (int i = 0; i < enemyCount; i++)
        {
            Transform point = await FindHiddenSpawnPointAsync(token);
            if (token.IsCancellationRequested) return;
            if (point == null || !SpawnWalker(i, point))
            {
                FailWave("NO VALID SPAWN POINT");
                return;
            }
            int burstSize = _waveDefinition != null ? Mathf.Max(1, _waveDefinition.burstSize) : 1;
            float delay = _waveDefinition != null && (i + 1) % burstSize == 0
                ? _waveDefinition.burstPause
                : spawnInterval;
            cancelled = await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0.05f, delay)), cancellationToken: token).SuppressCancellationThrow();
            if (cancelled) return;
        }
        _spawnFinished = true;
        TryCompleteWave();
    }

    int GetPrewarmType(int index)
    {
        if (_sources.Length <= 1) return 0;
        float ratio = (index + 0.5f) / Mathf.Max(1f, prewarmCount);
        int type = ratio < 0.40f ? 0 : ratio < 0.65f ? 1 : ratio < 0.85f ? 2 : 3;
        return Mathf.Min(type, _sources.Length - 1);
    }

    int SelectArchetype(int index)
    {
        if (_waveDefinition != null)
        {
            if (_waveDefinition.spawnBossAtEnd && index == enemyCount - 1)
                return Mathf.Min(3, _sources.Length - 1);
            Vector4 sourceWeights = _waveDefinition.archetypeWeights;
            Vector4 weights = new(Mathf.Max(0f, sourceWeights.x), Mathf.Max(0f, sourceWeights.y),
                Mathf.Max(0f, sourceWeights.z), Mathf.Max(0f, sourceWeights.w));
            float total = weights.x + weights.y + weights.z + weights.w;
            if (total > 0f)
            {
                float roll = UnityEngine.Random.value * total;
                if ((roll -= weights.x) <= 0f) return 0;
                if ((roll -= weights.y) <= 0f) return Mathf.Min(1, _sources.Length - 1);
                if ((roll -= weights.z) <= 0f) return Mathf.Min(2, _sources.Length - 1);
                return Mathf.Min(3, _sources.Length - 1);
            }
        }
        if (_sources.Length <= 1 || _difficultyWave <= 1) return 0;
        if (_difficultyWave == 2) return index % 4 == 0 ? Mathf.Min(1, _sources.Length - 1) : 0;
        if (_difficultyWave == 3)
            return index % 5 == 4 ? Mathf.Min(2, _sources.Length - 1) : index % 3 == 0 ? Mathf.Min(1, _sources.Length - 1) : 0;
        if (index == enemyCount - 1) return Mathf.Min(3, _sources.Length - 1);
        return index % 5 == 4 ? Mathf.Min(2, _sources.Length - 1) : index % 3 == 0 ? Mathf.Min(1, _sources.Length - 1) : 0;
    }

    EnemyHealth CreatePooledEnemy(int type)
    {
        type = Mathf.Clamp(type, 0, _sources.Length - 1);
        GameObject walker = Instantiate(_sources[type], transform);
        if (!walker.TryGetComponent(out EnemyHealth health))
            throw new InvalidOperationException("Zombie prefab requires EnemyHealth.");
        health.DespawnRequested += ReleaseEnemy;
        walker.SetActive(false);
        _all.Add(health);
        _poolIndex[health] = type;
        return health;
    }

    async UniTask<Transform> FindHiddenSpawnPointAsync(CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < hiddenSpawnMaxWait)
        {
            Transform hidden = SelectSpawnPoint(true);
            if (hidden != null) return hidden;
            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0.05f, hiddenSpawnRetryDelay)), cancellationToken: token).SuppressCancellationThrow();
            if (cancelled) return null;
            elapsed += Mathf.Max(0.05f, hiddenSpawnRetryDelay);
        }
        return SelectSpawnPoint(false);
    }

    Transform SelectSpawnPoint(bool hiddenOnly)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;
        Camera camera = Camera.main;
        // Without the player camera neither distance nor hiddenness can be verified.
        if (camera == null) return null;
        Transform player = camera.transform;
        List<Transform> preferredCandidates = new();
        Transform best = null;
        Transform closestFallback = null;
        float bestScore = float.MaxValue;
        float closestFallbackDistance = float.MaxValue;
        int startCursor = _spawnPointCursor;
        for (int offset = 0; offset < spawnPoints.Length; offset++)
        {
            int index = (startCursor + offset) % spawnPoints.Length;
            Transform point = spawnPoints[index];
            if (point == null) continue;
            Vector3 position = GetSpawnPosition(point);
            float distance = Vector3.Distance(player.position, position);
            if (distance < minimumSpawnDistance) continue;
            bool visible = IsVisibleFromCamera(camera, position + Vector3.up);
            if (hiddenOnly && visible) continue;

            if (distance < closestFallbackDistance)
            {
                closestFallback = point;
                closestFallbackDistance = distance;
            }

            if (distance > maximumSpawnDistance) continue;
            float score = Mathf.Abs(distance - preferredSpawnDistance);
            if (score <= 9f) preferredCandidates.Add(point);
            if (score < bestScore)
            {
                best = point;
                bestScore = score;
            }
        }
        if (preferredCandidates.Count > 0)
        {
            Transform selected = preferredCandidates[_spawnPointCursor % preferredCandidates.Count];
            _spawnPointCursor = (Array.IndexOf(spawnPoints, selected) + 1) % spawnPoints.Length;
            return selected;
        }
        Transform fallback = best != null ? best : closestFallback;
        if (fallback != null) _spawnPointCursor = (Array.IndexOf(spawnPoints, fallback) + 1) % spawnPoints.Length;
        if (fallback != null) return fallback;
        return null;
    }

    static bool IsVisibleFromCamera(Camera camera, Vector3 worldPosition)
    {
        Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
        if (viewport.z <= 0f || viewport.x < -0.05f || viewport.x > 1.05f || viewport.y < -0.05f || viewport.y > 1.05f)
            return false;
        Vector3 origin = camera.transform.position;
        Vector3 direction = worldPosition - origin;
        return !Physics.Raycast(origin, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
    }

    static Vector3 GetSpawnPosition(Transform point)
    {
        Vector3 position = point.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) position = hit.position;
        return position;
    }

    bool SpawnWalker(int index, Transform point)
    {
        if (point == null || Camera.main == null) return false;
        Vector3 position = GetSpawnPosition(point);
        if (Vector3.Distance(Camera.main.transform.position, position) < minimumSpawnDistance) return false;

        int archetype = SelectArchetype(index);
        EnemyHealth health = _pools[archetype].Count > 0 ? _pools[archetype].Dequeue() : CreatePooledEnemy(archetype);
        health.transform.SetPositionAndRotation(position, point.rotation);
        bool boss = (_waveDefinition != null ? _waveDefinition.spawnBossAtEnd : _difficultyWave >= 4) && index == enemyCount - 1;
        if (boss) BossActive = true;
        health.name = boss ? "BOSS_Arm_Overlord" : $"Infected_W{_difficultyWave}_{index + 1:00}_T{archetype}";
        health.gameObject.SetActive(true);

        float healthMultiplier = _waveDefinition != null
            ? _waveDefinition.healthMultiplier
            : _difficultyWave switch { 1 => 0.8f, 2 => 1f, 3 => 1.2f, _ => 1.45f };
        health.ResetForSpawn(healthMultiplier * (boss ? 10f : 1f));
        if (health.TryGetComponent(out ZombieAI ai))
        {
            ai.ConfigureDifficulty(_difficultyWave, index, boss,
                _waveDefinition != null ? _waveDefinition.speedMultiplier : 1f,
                _waveDefinition != null ? _waveDefinition.damageMultiplier : 1f);
            ai.ConfigureTarget(_waveDefinition != null ? _waveDefinition.targetMode : ZombieAI.TargetMode.Player);
        }

        _spawnedCount++;
        _alive.Add(health);
        health.Died += OnWalkerDied;
        return true;
    }

    void OnWalkerDied(EnemyHealth health)
    {
        if (health.name.StartsWith("BOSS_", StringComparison.Ordinal)) BossActive = false;
        float chance = _waveDefinition != null ? _waveDefinition.ammoDropChance : 0.15f;
        EnemyKilled?.Invoke(health.transform.position, chance);
        health.Died -= OnWalkerDied;
        _alive.Remove(health);
        TryCompleteWave();
    }

    void ReleaseEnemy(EnemyHealth health)
    {
        if (health == null || !health.gameObject.activeSelf) return;
        health.gameObject.SetActive(false);
        int type = _poolIndex.TryGetValue(health, out int index) ? index : 0;
        _pools[Mathf.Clamp(type, 0, _pools.Count - 1)].Enqueue(health);
    }

    void TryCompleteWave()
    {
        if (!_started || !_spawnFinished || _alive.Count > 0) return;
        _started = false;
        WaveCompleted?.Invoke();
    }

    public void CancelWave()
    {
        _spawnCts?.Cancel();
        _started = false;
        _spawnFinished = false;
        _spawnedCount = 0;
        BossActive = false;
        foreach (EnemyHealth health in _alive.ToArray()) ReleaseEnemy(health);
        _alive.Clear();
    }

    void FailWave(string reason)
    {
        // Exhausted retries are a terminal configuration failure, never a fake clear
        // or a wave waiting forever for enemies that were not spawned.
        CancelWave();
        Debug.LogWarning($"[GateWaveSpawner] {reason}", this);
        WaveFailed?.Invoke(reason);
    }

    void OnDestroy()
    {
        _spawnCts?.Cancel();
        _spawnCts?.Dispose();
        foreach (EnemyHealth health in _all)
            if (health != null) health.DespawnRequested -= ReleaseEnemy;
    }
}
