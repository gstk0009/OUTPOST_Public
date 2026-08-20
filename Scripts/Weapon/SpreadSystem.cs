using UnityEngine;

/// <summary>
/// 크로스헤어 및 사격 정확도를 위한 탄 퍼짐 상태 관리.
/// WeaponManager 또는 CrosshairUI가 참조해서 사용.
/// </summary>
public class SpreadSystem : MonoBehaviour
{
    [Header("Spread Multipliers")]
    public float standMult     = 1.0f;
    public float walkMult      = 1.6f;
    public float sprintMult    = 2.35f;
    public float sneakMult     = 1.2f;
    public float crouchMult    = 0.8f;
    public float jumpMult      = 3.5f;
    public float landedMult    = 2.0f;  // 착지 직후

    [Header("References")]
    public CrosshairUI crosshairUI;
    public WeaponManager weaponManager;

    private PlayerController _player;

    void Awake()
    {
        _player = GetComponentInParent<PlayerController>();
        if (_player == null) _player = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
        if (crosshairUI == null || _player == null) return;

        float mult = GetCurrentMultiplier();
        // 정규화 (1.0 = 기본, 최대 약 3.5배)
        float spreadFactor = Mathf.InverseLerp(standMult, jumpMult, mult);
        crosshairUI.SetSpread(spreadFactor);
    }

    public float GetCurrentMultiplier()
    {
        if (_player == null) return 1f;

        if (!_player.isGrounded) return jumpMult;
        if (_player.WasJustLanded) return landedMult;
        if (_player.isCrouching) return crouchMult;
        if (_player.isSneaking) return sneakMult;
        if (_player.isSprinting) return sprintMult;
        if (_player.IsMoving) return walkMult;

        return standMult;
    }
}
