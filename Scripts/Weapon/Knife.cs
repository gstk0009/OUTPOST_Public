using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Knife : WeaponBase
{
    public float slashRange = 2.1f;
    public float slashRadius = 0.32f;
    public float slashDelay = 0.11f;
    public float slashCooldown = 0.48f;

    private bool _isSlashing;
    private Vector3 _idlePosition;
    private Quaternion _idleRotation;
    private TrailRenderer _slashTrail;
    private CancellationTokenSource _slashCts;

    protected override void Awake()
    {
        base.Awake();
        slotIndex = 4;
        damage = 80f;
        isAutomatic = false;
        _idlePosition = transform.localPosition;
        _idleRotation = transform.localRotation;
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
            if (trail.name == "Knife_Blade") { _slashTrail = trail; break; }
        if (_slashTrail != null) _slashTrail.emitting = false;
    }

    public override void Fire() => BeginSlash(false);
    public override void AltFire() => BeginSlash(true);

    void BeginSlash(bool heavy)
    {
        if (_isSlashing) return;
        _slashCts?.Cancel();
        _slashCts?.Dispose();
        _slashCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        SlashAsync(heavy, _slashCts.Token).Forget();
    }

    async UniTaskVoid SlashAsync(bool heavy, CancellationToken token)
    {
        _isSlashing = true;
        if (_slashTrail != null) { _slashTrail.Clear(); _slashTrail.emitting = true; }

        Vector3 windupPosition = _idlePosition + (heavy ? new Vector3(0.16f, -0.12f, -0.20f) : new Vector3(0.10f, -0.06f, -0.12f));
        Quaternion windupRotation = _idleRotation * Quaternion.Euler(heavy ? 10f : 3f, heavy ? 18f : -8f, heavy ? -12f : -15f);
        if (!await MovePoseAsync(_idlePosition, _idleRotation, windupPosition, windupRotation, slashDelay, token)) return;

        Vector3 strikePosition = _idlePosition + (heavy ? new Vector3(-0.03f, 0.06f, 0.38f) : new Vector3(-0.18f, 0.10f, 0.20f));
        Quaternion strikeRotation = _idleRotation * Quaternion.Euler(heavy ? -12f : -5f, heavy ? -8f : -18f, heavy ? 12f : 42f);
        float strikeDuration = heavy ? 0.14f : 0.1f;
        if (!await MovePoseAsync(windupPosition, windupRotation, strikePosition, strikeRotation, strikeDuration, token)) return;
        ApplyKnifeHit(heavy);
        if (_slashTrail != null) _slashTrail.emitting = false;

        bool cancelled = await UniTask.Delay(40, cancellationToken: token).SuppressCancellationThrow();
        if (cancelled) return;
        if (!await MovePoseAsync(strikePosition, strikeRotation, _idlePosition, _idleRotation, heavy ? 0.2f : 0.15f, token)) return;
        int remainingMs = Mathf.Max(0, (int)((slashCooldown - slashDelay - strikeDuration - 0.14f) * 1000f));
        if (remainingMs > 0) await UniTask.Delay(remainingMs, cancellationToken: token).SuppressCancellationThrow();
        RestoreIdle();
        _isSlashing = false;
    }

    void ApplyKnifeHit(bool heavy)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        RaycastHit[] hits = Physics.SphereCastAll(cam.transform.position, slashRadius, cam.transform.forward,
            slashRange, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (player != null && hit.collider.transform.IsChildOf(player.transform)) continue;
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null) ReportMeleeHit(hit, damageable, heavy ? damage * 2f : damage);
            else CreateKnifeImpact(hit);
            break;
        }
    }

    void CreateKnifeImpact(RaycastHit hit)
    {
        if (CombatFxPool.Instance == null) return;
        GameObject impact = CombatFxPool.Instance.Rent(CombatFxPool.FxType.Impact);
        impact.transform.SetPositionAndRotation(hit.point + hit.normal * 0.015f, Quaternion.LookRotation(hit.normal));
        impact.TryGetComponent(out ParticleSystem particles);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.08f;
        main.startLifetime = 0.08f;
        main.startSpeed = 0.4f;
        main.startSize = 0.04f;
        main.startColor = new Color(1f, 0.7f, 0.18f);
        particles.Play(true);
        CombatFxPool.Instance.ReleaseAfter(impact, CombatFxPool.FxType.Impact, 0.1f);
    }

    async UniTask<bool> MovePoseAsync(Vector3 fromPosition, Quaternion fromRotation,
        Vector3 toPosition, Quaternion toRotation, float duration, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (token.IsCancellationRequested) return false;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
            transform.localPosition = Vector3.Lerp(fromPosition, toPosition, t);
            transform.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
        transform.localPosition = toPosition;
        transform.localRotation = toRotation;
        return true;
    }

    void RestoreIdle()
    {
        transform.localPosition = _idlePosition;
        transform.localRotation = _idleRotation;
        if (_slashTrail != null) _slashTrail.emitting = false;
    }

    void OnDisable()
    {
        _slashCts?.Cancel();
        _isSlashing = false;
        RestoreIdle();
    }

    void OnDestroy()
    {
        _slashCts?.Cancel();
        _slashCts?.Dispose();
    }
}
