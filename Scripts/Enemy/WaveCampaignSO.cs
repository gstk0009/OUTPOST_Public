using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WaveDefinition
{
    public string displayName = "웨이브";
    [Min(1)] public int enemyCount = 12;
    [Min(0f)] public float initialDelay = 0.5f;
    [Min(0.05f)] public float spawnInterval = 0.8f;
    [Min(1)] public int burstSize = 4;
    [Min(0f)] public float burstPause = 1.5f;
    [Tooltip("Basic / Ribcage / Chubby / Arm 가중치")]
    public Vector4 archetypeWeights = new(1f, 0f, 0f, 0f);
    public bool spawnBossAtEnd;
    [Min(0.1f)] public float healthMultiplier = 1f;
    [Min(0.1f)] public float speedMultiplier = 1f;
    [Min(0.1f)] public float damageMultiplier = 1f;
    [Range(0f, 1f)] public float ammoDropChance = 0.16f;
    public ZombieAI.TargetMode targetMode = ZombieAI.TargetMode.Player;

    [Header("Player-relative Spawn Range")]
    [Min(0f)] public float minimumSpawnDistance = 12f;
    [Min(0f)] public float preferredSpawnDistance = 22f;
    [Min(0f)] public float maximumSpawnDistance = 34f;
}

[CreateAssetMenu(fileName = "WaveCampaign", menuName = "Last Shift/Wave Campaign")]
public class WaveCampaignSO : ScriptableObject
{
    [Header("Flow")]
    [Min(0f)] public float preparationDuration = 8f;
    [Min(0f)] public float intermissionDuration = 7f;
    public List<WaveDefinition> waves = new();

    [Header("Hidden Spawn")]
    [Min(0f)] public float minimumSpawnDistance = 18f;
    [Min(0f)] public float hiddenSpawnRetryDelay = 0.25f;
    [Min(0f)] public float hiddenSpawnMaxWait = 2f;

    public WaveDefinition GetWave(int oneBasedIndex)
    {
        if (waves == null || waves.Count == 0) return null;
        return waves[Mathf.Clamp(oneBasedIndex - 1, 0, waves.Count - 1)];
    }
}
