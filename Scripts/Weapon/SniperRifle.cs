using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class SniperRifle : WeaponBase
{
    public float boltActionDelay = 1.2f;
    public float aimInDelay = 0.18f;
    public float aimOutDelay = 0.12f;
    public float[] zoomFOVs = { 60f, 30f, 15f };
    public float recoilVertical = 2.7f;
    public float recoilHorizontal = 0.22f;
    public float visualKickDistance = 0.13f;
    public float visualKickAngle = 7f;
    public Vector3 aimLocalPosition = new(-0.113f, -0.006f, 0.16f);
    public Vector3 aimLocalEulerOffset = Vector3.zero;
    public Vector3 boltLocalOffset = new(0.24f, -0.07f, -0.05f);
    public Vector3 boltLocalEulerOffset = new(2f, -5f, 9f);
    [Range(1f, 1.5f)] public float aimScaleMultiplier = 1.24f;
    public AudioClip boltCycleSound;
    [Range(0f, 1f)] public float boltCycleVolume = 0.82f;

    private int _currentZoomLevel;
    private int _restoreZoomLevel;
    private bool _isBusy;
    private Camera _mainCam;
    private SniperScopeUI _scopeUI;
    private Vector3 _readyPosition;
    private Quaternion _readyRotation;
    private Vector3 _readyScale;
    private Renderer[] _weaponRenderers;
    private CancellationTokenSource _actionCts;

    protected override void Awake()
    {
        base.Awake();
        slotIndex = 2;
        damage = 120f;
        maxAmmo = 10;
        currentAmmo = maxAmmo;
        reserveAmmo = 30;
        fireRate = boltActionDelay;
        baseSpread = 0.05f;
        moveSpreadMult = 5f;
        jumpSpreadMult = 8f;
        isSemiAutoWithHold = true;

        _mainCam = Camera.main;
        _scopeUI = FindFirstObjectByType<SniperScopeUI>(FindObjectsInactive.Include);
        _weaponRenderers = GetComponentsInChildren<Renderer>(true);
        _readyPosition = transform.localPosition;
        _readyRotation = transform.localRotation;
        _readyScale = transform.localScale;
        ApplyUnscopedView();
    }

    public override void Fire()
    {
        if (Time.time < nextFireTime || isReloading || _isBusy) return;
        if (currentAmmo <= 0) { HandleEmptyTrigger(); return; }

        bool wasScoped = _currentZoomLevel > 0;
        _restoreZoomLevel = _currentZoomLevel;
        if (wasScoped) BeginUnscopedTransition();

        currentAmmo--;
        nextFireTime = Time.time + boltActionDelay;
        float spread = (wasScoped ? baseSpread * 0.3f : baseSpread * 4f) * GetMovementMultiplier();
        recoilSystem?.AddRecoil(recoilVertical, Random.Range(-recoilHorizontal, recoilHorizontal));
        ShootRaycast(spread);
        RestartActionToken();
        ShotCycleAsync(wasScoped, _actionCts.Token).Forget();
    }

    public override void AltFire()
    {
        if (_isBusy || isReloading) return;
        if (_mainCam == null) _mainCam = Camera.main;
        if (_mainCam == null) return;

        if (_currentZoomLevel == 1) { ApplyScopedView(2); return; }
        RestartActionToken();
        if (_currentZoomLevel == 0) AimInAsync(1, _actionCts.Token).Forget();
        else AimOutAsync(_actionCts.Token).Forget();
    }

    protected override string[] GetMuzzleParentNames() => new[] { "svd_base", "sniper rifle 1", "sniper rifle 1_hands" };

    public void CaptureReadyPose()
    {
        _readyPosition = transform.localPosition;
        _readyRotation = transform.localRotation;
        _readyScale = transform.localScale;
    }

    async UniTaskVoid ShotCycleAsync(bool restoreScope, CancellationToken token)
    {
        _isBusy = true;
        if (animator != null) animator.enabled = false;

        Vector3 startPosition = transform.localPosition;
        Quaternion startRotation = transform.localRotation;
        Vector3 startScale = transform.localScale;
        Vector3 kickPosition = startPosition + Vector3.back * visualKickDistance;
        Quaternion kickRotation = startRotation * Quaternion.Euler(-visualKickAngle, 0f, -2f);
        if (!await MoveAsync(startPosition, startRotation, startScale, kickPosition, kickRotation,
                restoreScope ? _readyScale * aimScaleMultiplier : _readyScale, 0.07f, zoomFOVs[0], token)) return;

        Vector3 boltPosition = _readyPosition + boltLocalOffset;
        Quaternion boltRotation = _readyRotation * Quaternion.Euler(boltLocalEulerOffset);
        if (!await MoveAsync(kickPosition, kickRotation, transform.localScale, boltPosition, boltRotation,
                _readyScale, 0.14f, zoomFOVs[0], token)) return;

        gunAudio?.PlayUtilitySound(boltCycleSound, boltCycleVolume);
        Vector3 clickPosition = boltPosition + new Vector3(-0.035f, 0.025f, -0.035f);
        Quaternion clickRotation = boltRotation * Quaternion.Euler(-3f, 4f, -6f);
        if (!await MoveAsync(boltPosition, boltRotation, _readyScale, clickPosition, clickRotation,
                _readyScale, 0.08f, zoomFOVs[0], token)) return;
        if (!await MoveAsync(clickPosition, clickRotation, _readyScale, boltPosition, boltRotation,
                _readyScale, 0.08f, zoomFOVs[0], token)) return;

        bool cancelled = await UniTask.Delay(Mathf.Max(80, (int)((boltActionDelay - 0.57f) * 1000f)), cancellationToken: token).SuppressCancellationThrow();
        if (cancelled) return;

        Vector3 returnPosition = restoreScope ? aimLocalPosition : _readyPosition;
        Quaternion returnRotation = restoreScope ? _readyRotation * Quaternion.Euler(aimLocalEulerOffset) : _readyRotation;
        Vector3 returnScale = restoreScope ? _readyScale * aimScaleMultiplier : _readyScale;
        float returnFov = restoreScope ? zoomFOVs[Mathf.Clamp(_restoreZoomLevel, 1, zoomFOVs.Length - 1)] : zoomFOVs[0];
        if (!await MoveAsync(boltPosition, boltRotation, _readyScale, returnPosition, returnRotation,
                returnScale, restoreScope ? aimInDelay : 0.18f, returnFov, token)) return;

        if (restoreScope) ApplyScopedView(_restoreZoomLevel);
        else
        {
            RestoreReadyPose();
            PlayIdleAnimation();
        }
        _isBusy = false;
    }

    async UniTaskVoid AimInAsync(int level, CancellationToken token)
    {
        _isBusy = true;
        BeginUnscopedTransition();
        if (animator != null) animator.enabled = false;
        int index = Mathf.Clamp(level, 1, zoomFOVs.Length - 1);
        bool completed = await MoveAsync(transform.localPosition, transform.localRotation, transform.localScale,
            aimLocalPosition, _readyRotation * Quaternion.Euler(aimLocalEulerOffset), _readyScale * aimScaleMultiplier,
            aimInDelay, zoomFOVs[index], token);
        if (completed) ApplyScopedView(level);
        _isBusy = false;
    }

    async UniTaskVoid AimOutAsync(CancellationToken token)
    {
        _isBusy = true;
        BeginUnscopedTransition();
        if (animator != null) animator.enabled = false;
        bool completed = await MoveAsync(transform.localPosition, transform.localRotation, transform.localScale,
            _readyPosition, _readyRotation, _readyScale, aimOutDelay, zoomFOVs[0], token);
        if (completed) { RestoreReadyPose(); PlayIdleAnimation(); }
        _isBusy = false;
    }

    async UniTask<bool> MoveAsync(Vector3 fromPosition, Quaternion fromRotation, Vector3 fromScale,
        Vector3 toPosition, Quaternion toRotation, Vector3 toScale, float duration, float toFov, CancellationToken token)
    {
        float fromFov = _mainCam != null ? _mainCam.fieldOfView : zoomFOVs[0];
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            if (token.IsCancellationRequested) return false;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / safeDuration));
            transform.localPosition = Vector3.Lerp(fromPosition, toPosition, t);
            transform.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);
            transform.localScale = Vector3.Lerp(fromScale, toScale, t);
            if (_mainCam != null) _mainCam.fieldOfView = Mathf.Lerp(fromFov, toFov, t);
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
        transform.localPosition = toPosition;
        transform.localRotation = toRotation;
        transform.localScale = toScale;
        if (_mainCam != null) _mainCam.fieldOfView = toFov;
        return true;
    }

    protected override async UniTask ReloadAsync(CancellationToken token)
    {
        if (isReloading || _isBusy) return;
        isReloading = true;
        _isBusy = true;
        BeginUnscopedTransition();
        if (animator != null) { animator.enabled = true; animator.SetTrigger("Reload"); }
        gunAudio?.PlayReloadSound();

        Vector3 reloadPosition = _readyPosition + new Vector3(0.23f, -0.30f, -0.10f);
        Quaternion reloadRotation = _readyRotation * Quaternion.Euler(18f, -24f, 18f);
        if (!await MoveAsync(transform.localPosition, transform.localRotation, transform.localScale,
                reloadPosition, reloadRotation, _readyScale, 0.22f, zoomFOVs[0], token)) return;
        bool cancelled = await UniTask.Delay(Mathf.Max(100, (int)((reloadTime - 0.5f) * 1000f)), cancellationToken: token).SuppressCancellationThrow();
        if (cancelled) return;
        if (!await MoveAsync(reloadPosition, reloadRotation, _readyScale, _readyPosition, _readyRotation,
                _readyScale, 0.28f, zoomFOVs[0], token)) return;

        int take = Mathf.Min(maxAmmo - currentAmmo, reserveAmmo);
        currentAmmo += take;
        reserveAmmo -= take;
        RestoreReadyPose();
        PlayIdleAnimation();
        isReloading = false;
        _isBusy = false;
    }

    void ApplyScopedView(int level)
    {
        _currentZoomLevel = Mathf.Clamp(level, 1, zoomFOVs.Length - 1);
        if (_mainCam != null) _mainCam.fieldOfView = zoomFOVs[_currentZoomLevel];
        _scopeUI?.ShowScope(_currentZoomLevel);
        SetWeaponRenderers(false);
    }

    void ApplyUnscopedView()
    {
        _currentZoomLevel = 0;
        if (_mainCam != null) _mainCam.fieldOfView = zoomFOVs[0];
        _scopeUI?.HideScope();
        SetWeaponRenderers(true);
    }

    void BeginUnscopedTransition()
    {
        _currentZoomLevel = 0;
        _scopeUI?.HideScope();
        SetWeaponRenderers(true);
    }

    void SetWeaponRenderers(bool visible)
    {
        foreach (Renderer target in _weaponRenderers)
            if (target != null) target.enabled = visible;
    }

    void PlayIdleAnimation()
    {
        if (animator == null) return;
        animator.enabled = true;
        int idleHash = Animator.StringToHash("Base Layer.Idle");
        if (animator.HasState(0, idleHash)) animator.CrossFade(idleHash, 0.04f);
    }

    void RestoreReadyPose()
    {
        transform.localPosition = _readyPosition;
        transform.localRotation = _readyRotation;
        transform.localScale = _readyScale;
    }

    void RestartActionToken()
    {
        _actionCts?.Cancel();
        _actionCts?.Dispose();
        _actionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
    }

    void OnDisable()
    {
        _actionCts?.Cancel();
        _isBusy = false;
        isReloading = false;
        RestoreReadyPose();
        SetWeaponRenderers(true);
        ApplyUnscopedView();
    }

    void OnDestroy()
    {
        _actionCts?.Cancel();
        _actionCts?.Dispose();
    }
}
