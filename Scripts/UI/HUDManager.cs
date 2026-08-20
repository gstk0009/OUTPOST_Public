using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>인게임 HUD 전체 관리 (탄약, 체력, 킬로그)</summary>
public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Ammo")]
    public TextMeshProUGUI ammoCurrentText;
    public TextMeshProUGUI ammoReserveText;
    public TextMeshProUGUI magazineCountText;

    [Header("Health")]
    public Slider healthBar;
    public TextMeshProUGUI healthText;
    public HUDIconGraphic healthIcon;

    [Header("Killlog")]
    public Transform killLogParent;
    public GameObject killLogEntryPrefab;
    public float killLogDuration = 4f;

    [Header("References")]
    public WeaponManager weaponManager;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        UpdateAmmoDisplay();
    }

    void UpdateAmmoDisplay()
    {
        if (weaponManager == null) return;

        WeaponBase weapon = weaponManager.GetCurrentWeapon();
        if (weapon == null || ammoCurrentText == null) return;

        // 칼/수류탄은 특수 표시
        if (weapon.slotIndex == 4)
        {
            ammoCurrentText.text = "--";
            if (ammoReserveText) ammoReserveText.text = "";
            if (magazineCountText) magazineCountText.text = "근접";
        }
        else
        {
            ammoCurrentText.text = weapon.currentAmmo.ToString();
            if (ammoReserveText) ammoReserveText.text = weapon.reserveAmmo.ToString();
            if (magazineCountText)
            {
                int magazines = weapon.maxAmmo > 0
                    ? Mathf.CeilToInt(weapon.reserveAmmo / (float)weapon.maxAmmo)
                    : 0;
                magazineCountText.text = "탄창 " + magazines;
            }
        }
    }

    public void UpdateHealth(float current, float max)
    {
        float ratio = max > 0f ? current / max : 0f;
        if (healthBar) healthBar.value = ratio;
        if (healthText) healthText.text = Mathf.CeilToInt(current).ToString();
        if (healthIcon)
        {
            healthIcon.color = ratio <= 0.3f
                ? new Color(1f, 0.22f, 0.18f)
                : ratio <= 0.6f
                    ? new Color(1f, 0.72f, 0.18f)
                    : new Color(0.24f, 0.95f, 0.48f);
        }
    }

    public void AddKillLog(string killerName, string victimName)
    {
        if (killLogEntryPrefab == null || killLogParent == null) return;

        GameObject entry = Instantiate(killLogEntryPrefab, killLogParent);
        var text = entry.GetComponentInChildren<TextMeshProUGUI>();
        if (text) text.text = $"{killerName}  >  {victimName}";

        Destroy(entry, killLogDuration);
    }
}
