using System.Collections.Generic;
using UnityEngine;

public class HealthPickupSpawner : MonoBehaviour
{
    public GateWaveSpawner waveSpawner;
    public HealthPickup pickupPrefab;
    public Transform[] fixedSpawnPoints;
    [Min(0)] public int prewarmCount = 5;
    [Range(0f, 1f)] public float enemyDropChance = 0.08f;
    [Min(0f)] public float pickupHeight = 0.55f;

    readonly Queue<HealthPickup> _pool = new();

    void Awake()
    {
        if (waveSpawner == null) waveSpawner = FindFirstObjectByType<GateWaveSpawner>();
        if (pickupPrefab == null) return;
        for (int i = 0; i < prewarmCount; i++) _pool.Enqueue(Create());
    }

    void Start()
    {
        if (waveSpawner != null) waveSpawner.EnemyKilled += OnEnemyKilled;
        if (fixedSpawnPoints == null) return;
        foreach (Transform point in fixedSpawnPoints)
            if (point != null) SpawnAt(point.position);
    }

    HealthPickup Create()
    {
        HealthPickup pickup = Instantiate(pickupPrefab, transform);
        pickup.gameObject.SetActive(false);
        pickup.Collected += Release;
        return pickup;
    }

    void OnEnemyKilled(Vector3 position, float unusedAmmoChance)
    {
        if (Random.value <= enemyDropChance) SpawnAt(position);
    }

    void SpawnAt(Vector3 position)
    {
        if (pickupPrefab == null) return;
        HealthPickup pickup = _pool.Count > 0 ? _pool.Dequeue() : Create();
        pickup.transform.SetPositionAndRotation(position + Vector3.up * pickupHeight, Quaternion.identity);
        pickup.gameObject.SetActive(true);
    }

    void Release(HealthPickup pickup)
    {
        if (pickup == null || !pickup.gameObject.activeSelf) return;
        pickup.gameObject.SetActive(false);
        _pool.Enqueue(pickup);
    }

    void OnDestroy()
    {
        if (waveSpawner != null) waveSpawner.EnemyKilled -= OnEnemyKilled;
        foreach (HealthPickup pickup in _pool)
            if (pickup != null) pickup.Collected -= Release;
    }
}
