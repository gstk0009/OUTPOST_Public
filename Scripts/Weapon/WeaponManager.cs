using Cysharp.Threading.Tasks;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapon Slots: 1 Rifle / 2 Sniper / 3 Pistol")]
    public WeaponBase[] weapons = new WeaponBase[3];
    public int currentWeaponIndex;

    [Header("Scene References")]
    public Transform weaponHolder;
    public WeaponSlotUI slotUI;

    [Header("Settings")]
    public float swapTime = 0.35f;
    public bool autoBindSceneWeapons = true;
    public bool logWeaponBinding;

    private bool _isSwapping;

    void Awake()
    {
        if (weaponHolder == null) weaponHolder = FindWeaponHolder();
        if (autoBindSceneWeapons) BindSceneWeapons();
        ApplyRegisteredWeaponPoses();
    }

    void Start()
    {
        EnsureValidCurrentWeapon();
        ApplyWeaponVisibility();
        slotUI?.ConfigureAvailableSlots(weapons);
        slotUI?.ShowSlotChange(currentWeaponIndex);
    }

    void Update()
    {
        if (!HasAnyWeapon() || _isSwapping) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchWeapon(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchWeapon(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchWeapon(2);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) SwitchWeapon(GetAvailableWeaponIndex(-1));
        else if (scroll < 0f) SwitchWeapon(GetAvailableWeaponIndex(1));

        GetCurrentWeapon()?.HandleFireInput();
    }

    public void SwitchWeapon(int index)
    {
        if (!IsValidWeaponIndex(index) || index == currentWeaponIndex || _isSwapping) return;
        SwitchWeaponAsync(index).Forget();
    }

    async UniTaskVoid SwitchWeaponAsync(int index)
    {
        _isSwapping = true;
        WeaponBase current = GetCurrentWeapon();
        if (current != null) current.gameObject.SetActive(false);

        bool cancelled = await UniTask.Delay(60, cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (cancelled) { _isSwapping = false; return; }

        currentWeaponIndex = index;
        WeaponBase next = GetCurrentWeapon();
        if (next != null)
        {
            Vector3 readyPosition = next.transform.localPosition;
            Quaternion readyRotation = next.transform.localRotation;
            Vector3 readyScale = next.transform.localScale;
            Vector3 hiddenPosition = readyPosition + new Vector3(0.12f, -0.38f, -0.14f);
            Quaternion hiddenRotation = readyRotation * Quaternion.Euler(20f, -7f, 9f);

            Animator nextAnimator = next.animator;
            if (nextAnimator != null)
                nextAnimator.enabled = false;

            // Set the first visible pose before activation so the idle pose cannot flash for one frame.
            next.transform.localPosition = hiddenPosition;
            next.transform.localRotation = hiddenRotation;
            next.transform.localScale = readyScale * 0.96f;
            next.gameObject.SetActive(true);
            next.gunAudio?.PlayDraw();

            float duration = Mathf.Max(0.22f, swapTime);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
                next.transform.localPosition = Vector3.LerpUnclamped(hiddenPosition, readyPosition, t);
                next.transform.localRotation = Quaternion.Slerp(hiddenRotation, readyRotation, t);
                next.transform.localScale = Vector3.Lerp(readyScale * 0.96f, readyScale, t);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            next.transform.localPosition = readyPosition;
            next.transform.localRotation = readyRotation;
            next.transform.localScale = readyScale;
            if (next is SniperRifle sniper) sniper.CaptureReadyPose();
            PlayIdleAnimation(nextAnimator);
        }
        slotUI?.ShowSlotChange(currentWeaponIndex);
        _isSwapping = false;
    }

    public WeaponBase GetCurrentWeapon() => IsValidWeaponIndex(currentWeaponIndex) ? weapons[currentWeaponIndex] : null;

    public int AddAmmoMagazines(AmmoPickup.AmmoKind kind, int magazineCount)
    {
        int total = 0;
        foreach (WeaponBase weapon in weapons)
        {
            if (weapon == null || weapon is Knife) continue;
            bool matches = kind == AmmoPickup.AmmoKind.All ||
                           kind == AmmoPickup.AmmoKind.Rifle && weapon is AssaultRifle ||
                           kind == AmmoPickup.AmmoKind.Sniper && weapon is SniperRifle ||
                           kind == AmmoPickup.AmmoKind.Pistol && weapon is Pistol;
            if (matches) total += weapon.AddReserveAmmo(weapon.maxAmmo * Mathf.Max(1, magazineCount));
        }
        return total;
    }

    void BindSceneWeapons()
    {
        EnsureWeaponArray();
        for (int i = 0; i < weapons.Length; i++) weapons[i] = null;

        if (weaponHolder == null)
        {
            Debug.LogWarning("[WeaponManager] WeaponHolder was not found.");
            return;
        }

        foreach (Transform child in weaponHolder)
        {
            child.TryGetComponent(out WeaponBase weapon);
            if (weapon == null) continue;
            if (weapon is Knife)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            ApplyCompactPose(weapon.transform, weapon);
            ConfigureWeaponReferences(weapon);
            RegisterWeapon(weapon);
            if (logWeaponBinding)
                Debug.Log($"[WeaponManager] Slot {weapon.slotIndex}: {weapon.GetType().Name} / {weapon.name}");
        }
    }

    void RegisterWeapon(WeaponBase weapon)
    {
        if (weapon is AssaultRifle) weapons[0] = weapon;
        else if (weapon is SniperRifle) weapons[1] = weapon;
        else if (weapon is Pistol) weapons[2] = weapon;
    }

    void ConfigureWeaponReferences(WeaponBase weapon)
    {
        weapon.handleInputDirectly = false;
        weapon.spawnVisualBullet = false;
        if (weapon.animator == null) weapon.animator = weapon.GetComponentInChildren<Animator>();
        if (weapon.animator != null && weapon.animator.runtimeAnimatorController == null)
            weapon.animator = null;
        if (weapon.gunAudio == null) weapon.TryGetComponent(out weapon.gunAudio);
        if (weapon.recoilSystem == null) weapon.recoilSystem = GetComponentInChildren<RecoilSystem>();
        if (weapon is AssaultRifle rifle && rifle.weaponHolder == null) rifle.weaponHolder = weaponHolder;
        weapon.EnsureRuntimeFxSetup();
    }

    void ApplyRegisteredWeaponPoses()
    {
        if (weapons == null) return;
        foreach (WeaponBase weapon in weapons)
        {
            if (weapon == null) continue;
            if (weapon is Knife)
            {
                weapon.gameObject.SetActive(false);
                continue;
            }
            ApplyCompactPose(weapon.transform, weapon);
        }
    }

    static void ApplyCompactPose(Transform weaponTransform, WeaponBase weapon)
    {
        if (weapon is SniperRifle)
        {
            weaponTransform.localPosition = new Vector3(0.00f, -0.38f, 0.42f);
            weaponTransform.localScale = Vector3.one * 0.92f;
        }
        else if (weapon is AssaultRifle)
        {
            weaponTransform.localPosition = new Vector3(0.02f, -0.38f, 0.42f);
            weaponTransform.localScale = Vector3.one * 0.98f;
        }
        else if (weapon is Pistol)
        {
            weaponTransform.localPosition = new Vector3(0.04f, -0.40f, 0.42f);
            weaponTransform.localScale = Vector3.one;
        }
    }

    Transform FindWeaponHolder()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == "WeaponHolder") return child;
        return null;
    }

    void EnsureWeaponArray()
    {
        if (weapons != null && weapons.Length == 3) return;
        WeaponBase[] resized = new WeaponBase[3];
        if (weapons != null)
            for (int i = 0; i < Mathf.Min(weapons.Length, resized.Length); i++) resized[i] = weapons[i];
        weapons = resized;
    }

    void EnsureValidCurrentWeapon()
    {
        EnsureWeaponArray();
        if (IsValidWeaponIndex(currentWeaponIndex)) return;
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == null) continue;
            currentWeaponIndex = i;
            return;
        }
        currentWeaponIndex = 0;
    }

    void ApplyWeaponVisibility()
    {
        for (int i = 0; i < weapons.Length; i++)
            if (weapons[i] != null) weapons[i].gameObject.SetActive(i == currentWeaponIndex);
    }

    int GetAvailableWeaponIndex(int direction)
    {
        int index = currentWeaponIndex;
        for (int i = 0; i < weapons.Length; i++)
        {
            index = (index + direction + weapons.Length) % weapons.Length;
            if (weapons[index] != null) return index;
        }
        return currentWeaponIndex;
    }

    bool IsValidWeaponIndex(int index) => weapons != null && index >= 0 && index < weapons.Length && weapons[index] != null;

    bool HasAnyWeapon()
    {
        if (weapons == null) return false;
        foreach (WeaponBase weapon in weapons) if (weapon != null) return true;
        return false;
    }

    static void PlayIdleAnimation(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.enabled = true;
        int idleHash = Animator.StringToHash("Base Layer.Idle");
        if (animator.HasState(0, idleHash))
            animator.CrossFade(idleHash, 0.04f);
    }
}
