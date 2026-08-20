using UnityEngine;

public class Pistol : WeaponBase
{
    protected override void Awake()
    {
        base.Awake();
        slotIndex = 3;
        damage = 45f;
        maxAmmo = 12;
        currentAmmo = maxAmmo;
        reserveAmmo = 36;
        fireRate = 0.15f;
        baseSpread = 0.3f;
        moveSpreadMult = 1.8f;
        jumpSpreadMult = 3.0f;
        isAutomatic = false;
        isSemiAutoWithHold = false;
    }

    protected override string[] GetMuzzleParentNames()
    {
        return new[] { "Makarov_base", "pistol1_hands" };
    }

    public override void Fire()
    {
        if (Time.time < nextFireTime || isReloading) return;

        if (currentAmmo <= 0)
        {
            HandleEmptyTrigger();
            return;
        }

        currentAmmo--;
        nextFireTime = Time.time + fireRate;

        float spread = baseSpread * GetMovementMultiplier();
        ShootRaycast(spread);

        if (animator) animator.SetTrigger("Fire");
        if (recoilSystem) recoilSystem.AddRecoil(0.15f, 0f);
    }

    public override void AltFire()
    {
        // Pistol has no alternate fire.
    }
}
