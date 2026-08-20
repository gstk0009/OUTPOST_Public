using Cysharp.Threading.Tasks;
using UnityEngine;

public class Grenade : WeaponBase
{
    public GameObject grenadePrefab;
    public float throwForce = 15f;
    public float explosionRadius = 5f;
    public float explosionDelay = 3f;
    public float throwCooldown = 1f;
    private bool _isThrowing;

    protected override void Awake()
    {
        base.Awake();
        slotIndex = 4;
        maxAmmo = 2;
        currentAmmo = maxAmmo;
        reserveAmmo = 0;
        isAutomatic = false;
    }

    public override void Fire()
    {
        if (!_isThrowing && currentAmmo > 0) ThrowAsync().Forget();
    }

    public override void AltFire() { }

    async UniTaskVoid ThrowAsync()
    {
        _isThrowing = true;
        currentAmmo--;
        if (animator != null) animator.SetTrigger("Throw");
        bool cancelled = await UniTask.Delay(300, cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (cancelled) return;

        Camera cam = Camera.main;
        if (grenadePrefab != null && cam != null)
        {
            GameObject instance = Instantiate(grenadePrefab, cam.transform.position + cam.transform.forward * 0.5f, cam.transform.rotation);
            if (instance.TryGetComponent(out Rigidbody body)) body.linearVelocity = cam.transform.forward * throwForce;
            if (instance.TryGetComponent(out GrenadeProjectile projectile))
            {
                projectile.explosionRadius = explosionRadius;
                projectile.explosionDelay = explosionDelay;
                projectile.damage = damage;
            }
        }

        await UniTask.Delay((int)(throwCooldown * 1000f), cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        _isThrowing = false;
    }
}
