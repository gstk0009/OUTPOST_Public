using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    public float destroyDelay = 2.2f;
    public AudioClip[] hitSounds;
    public AudioClip[] deathSounds;
    public bool IsDead { get; private set; }
    public float CurrentHealth { get; private set; }
    public event Action<EnemyHealth> Died;
    public event Action<EnemyHealth> DespawnRequested;

    private AudioSource _audioSource;
    private ZombieAI _ai;
    private NavMeshAgent _agent;
    private Collider _hitbox;
    private Renderer[] _renderers;
    private CancellationTokenSource _flashCts;
    private static float _nextSharedImpactSoundTime;

    void Awake()
    {
        TryGetComponent(out _audioSource);
        TryGetComponent(out _ai);
        TryGetComponent(out _agent);
        TryGetComponent(out _hitbox);
        _renderers = GetComponentsInChildren<Renderer>(true);
        ResetForSpawn();
    }

    public void ResetForSpawn(float healthMultiplier = 1f)
    {
        IsDead = false;
        CurrentHealth = maxHealth * Mathf.Max(0.1f, healthMultiplier);
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (_hitbox != null) _hitbox.enabled = true;
        if (_agent != null) _agent.enabled = true;
        if (_ai != null) _ai.enabled = true;
        foreach (Renderer target in _renderers)
            if (target != null) target.SetPropertyBlock(null);
    }

    public void FlashHit(bool headshot)
    {
        _flashCts?.Cancel();
        _flashCts?.Dispose();
        _flashCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        FlashHitAsync(headshot, _flashCts.Token).Forget();
    }

    async UniTaskVoid FlashHitAsync(bool headshot, CancellationToken token)
    {
        Color color = headshot ? new Color(1f, 0.82f, 0.16f) : new Color(1f, 0.035f, 0.015f);
        foreach (Renderer target in _renderers)
        {
            if (target == null) continue;
            MaterialPropertyBlock block = new();
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color * 1.8f);
            target.SetPropertyBlock(block);
        }
        bool cancelled = await UniTask.Delay(120, cancellationToken: token).SuppressCancellationThrow();
        if (cancelled) return;
        foreach (Renderer target in _renderers)
            if (target != null) target.SetPropertyBlock(null);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        if (Time.time >= _nextSharedImpactSoundTime)
        {
            _nextSharedImpactSoundTime = Time.time + 0.035f;
            PlayRandom(hitSounds, 0.8f);
        }
        if (CurrentHealth > 0f) _ai?.PlayHitAnimation();
        if (CurrentHealth <= 0f) DieAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    async UniTaskVoid DieAsync(CancellationToken token)
    {
        if (IsDead) return;
        IsDead = true;
        GameSessionState.RegisterKill();
        Died?.Invoke(this);
        if (Time.time >= _nextSharedImpactSoundTime)
        {
            _nextSharedImpactSoundTime = Time.time + 0.06f;
            PlayRandom(deathSounds, 1f);
        }
        bool hasDeathAnimation = _ai != null && _ai.PlayDeathAnimation();

        if (_ai != null) _ai.enabled = false;
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        if (_agent != null) _agent.enabled = false;
        if (_hitbox != null) _hitbox.enabled = false;

        if (!hasDeathAnimation)
        {
            Quaternion from = transform.rotation;
            Quaternion to = from * Quaternion.Euler(82f, 0f, 18f);
            float elapsed = 0f;
            while (elapsed < 0.42f)
            {
                elapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / 0.42f));
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (token.IsCancellationRequested) return;
            }
        }
        else
        {
            bool animationWaitCancelled = await UniTask.Delay(650, cancellationToken: token).SuppressCancellationThrow();
            if (animationWaitCancelled) return;
        }

        ApplyDeadVisual();
        bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(1.5f, destroyDelay)), cancellationToken: token).SuppressCancellationThrow();
        if (!cancelled) DespawnRequested?.Invoke(this);
    }

    void ApplyDeadVisual()
    {
        Color deadColor = new(0.22f, 0.075f, 0.055f, 1f);
        foreach (Renderer target in _renderers)
        {
            if (target == null) continue;
            MaterialPropertyBlock block = new();
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", deadColor);
            block.SetColor("_EmissionColor", Color.black);
            target.SetPropertyBlock(block);
        }
    }

    void PlayRandom(AudioClip[] clips, float volume)
    {
        if (_audioSource == null || clips == null || clips.Length == 0) return;
        _audioSource.pitch = UnityEngine.Random.Range(0.94f, 1.05f);
        _audioSource.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)], volume);
    }

    void OnDestroy()
    {
        _flashCts?.Cancel();
        _flashCts?.Dispose();
    }
}
