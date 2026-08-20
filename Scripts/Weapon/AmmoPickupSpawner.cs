using System.Collections.Generic;
using UnityEngine;

public class AmmoPickupSpawner : MonoBehaviour
{
    public GateWaveSpawner waveSpawner;
    public AmmoPickup[] pickupPrefabs;
    public Transform[] fixedSpawnPoints;
    [Min(0)] public int prewarmPerType = 5;
    [Min(0f)] public float pickupHeight = 0.55f;
    [Range(0f, 1f)] public float rifleDropShare = 0.6f;

    private readonly List<Queue<AmmoPickup>> _pools = new();
    private readonly Dictionary<AmmoPickup, int> _poolTypes = new();

    void Awake()
    {
        if (waveSpawner == null) waveSpawner = FindFirstObjectByType<GateWaveSpawner>();
        for (int type = 0; type < pickupPrefabs.Length; type++)
        {
            _pools.Add(new Queue<AmmoPickup>());
            for (int i = 0; i < prewarmPerType; i++) _pools[type].Enqueue(Create(type));
        }
    }

    void Start()
    {
        if (waveSpawner != null) waveSpawner.EnemyKilled += OnEnemyKilled;
        for (int i = 0; i < fixedSpawnPoints.Length; i++)
            SpawnAt(fixedSpawnPoints[i].position, i % Mathf.Max(1, pickupPrefabs.Length));
    }

    AmmoPickup Create(int type)
    {
        AmmoPickup pickup = Instantiate(pickupPrefabs[type], transform);
        pickup.gameObject.SetActive(false);
        pickup.Collected += Release;
        _poolTypes[pickup] = type;
        return pickup;
    }

    void OnEnemyKilled(Vector3 position, float dropChance)
    {
        if (pickupPrefabs.Length == 0 || Random.value > dropChance) return;
        SpawnAt(position, ChooseDropType());
    }

    int ChooseDropType()
    {
        var rifleTypes = new List<int>();
        var otherTypes = new List<int>();
        for (int i = 0; i < pickupPrefabs.Length; i++)
        {
            if (pickupPrefabs[i] != null && pickupPrefabs[i].ammoKind == AmmoPickup.AmmoKind.Rifle)
                rifleTypes.Add(i);
            else
                otherTypes.Add(i);
        }

        bool chooseRifle = rifleTypes.Count > 0 && (otherTypes.Count == 0 || Random.value < rifleDropShare);
        List<int> candidates = chooseRifle ? rifleTypes : otherTypes;
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : Random.Range(0, pickupPrefabs.Length);
    }

    void SpawnAt(Vector3 position, int type)
    {
        if (pickupPrefabs.Length == 0) return;
        type = Mathf.Clamp(type, 0, pickupPrefabs.Length - 1);
        AmmoPickup pickup = _pools[type].Count > 0 ? _pools[type].Dequeue() : Create(type);
        pickup.transform.SetPositionAndRotation(position + Vector3.up * pickupHeight, Quaternion.identity);
        pickup.gameObject.SetActive(true);
    }

    void Release(AmmoPickup pickup)
    {
        if (pickup == null || !pickup.gameObject.activeSelf) return;
        pickup.gameObject.SetActive(false);
        _pools[_poolTypes[pickup]].Enqueue(pickup);
    }

    void OnDestroy()
    {
        if (waveSpawner != null) waveSpawner.EnemyKilled -= OnEnemyKilled;
        foreach (Queue<AmmoPickup> pool in _pools)
            foreach (AmmoPickup pickup in pool)
                if (pickup != null) pickup.Collected -= Release;
    }
}
