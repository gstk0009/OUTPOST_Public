using UnityEngine;

public class AssaultRifle : WeaponBase
{
    [Header("Rifle Specific")]
    public float recoilVertical = 0.3f;
    public float recoilHorizontalMax = 0.15f;
    public float recoilRecovery = 5f;
    public float spreadIncreasePerShot = 0.02f;
    public float spreadDecayRate = 3f;

    [Header("Weapon Bob")]
    public Transform weaponHolder;
    public float bobFrequency = 12f;
    public float bobAmplitude = 0.02f;
    private Vector3 _baseHolderPos;

    private int _consecutiveShots = 0;
    private float _accumulatedSpread = 0f;
    private float _lastShotTime;

    protected override void Awake()
    {
        base.Awake();
        slotIndex = 1;
        damage = 35f;
        maxAmmo = 30;
        currentAmmo = maxAmmo;
        reserveAmmo = 90;
        fireRate = 0.09f;
        baseSpread = 0.18f;
        moveSpreadMult = 2.0f;
        jumpSpreadMult = 3.5f;
        isAutomatic = true;

        if (weaponHolder) _baseHolderPos = weaponHolder.localPosition;
    }

    protected override string[] GetMuzzleParentNames()
    {
        return new[] { "Ak47", "ar1(automatic rifle)_hands" };
    }

    void LateUpdate()
    {
        if (Time.time - _lastShotTime > fireRate * 2f)
        {
            _consecutiveShots = 0;
            _accumulatedSpread = Mathf.Lerp(_accumulatedSpread, 0f, spreadDecayRate * Time.deltaTime);
        }

        if (weaponHolder != null && player != null && player.IsMoving && player.isGrounded)
        {
            float freq = player.isSneaking ? bobFrequency * 0.7f : bobFrequency;
            float amp = player.isSneaking ? bobAmplitude * 0.5f : bobAmplitude;
            float bobOffset = Mathf.Sin(Time.time * freq) * amp;
            weaponHolder.localPosition = _baseHolderPos + new Vector3(0f, bobOffset, 0f);
        }
        else if (weaponHolder != null)
        {
            weaponHolder.localPosition = Vector3.Lerp(weaponHolder.localPosition, _baseHolderPos, 10f * Time.deltaTime);
        }
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
        _consecutiveShots++;

        ApplyRecoil();

        _accumulatedSpread += spreadIncreasePerShot;
        float totalSpread = (baseSpread + _accumulatedSpread) * GetMovementMultiplier();
        ShootRaycast(totalSpread);

        if (animator) animator.SetTrigger("Fire");

        _lastShotTime = Time.time;
    }

    public override void AltFire()
    {
        // Rifle has no alternate fire yet.
    }

    void ApplyRecoil()
    {
        if (recoilSystem == null) return;

        float verticalKick = recoilVertical;
        float horizontalKick = 0f;

        if (_consecutiveShots > 4)
        {
            float t = Mathf.Clamp01((_consecutiveShots - 4f) / 10f);
            horizontalKick = Random.Range(-recoilHorizontalMax, recoilHorizontalMax) * t;
        }

        recoilSystem.AddRecoil(verticalKick, horizontalKick);
    }
}
