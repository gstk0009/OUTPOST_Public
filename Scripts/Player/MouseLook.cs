using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("Sensitivity")]
    public float mouseSensitivity = 1.65f;
    public float lookSharpness = 42f;

    [Header("References")]
    public Transform playerBody;    // 플레이어 루트 오브젝트 (Y축 회전)

    private float _xRotation = 0f;
    private Vector2 _smoothedDelta;
    private bool _locked = true;

    void Start()
    {
        LockCursor(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            LockCursor(!_locked);

        if (!_locked) return;

        Vector2 rawDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * mouseSensitivity;
        float blend = 1f - Mathf.Exp(-lookSharpness * Time.unscaledDeltaTime);
        _smoothedDelta = Vector2.Lerp(_smoothedDelta, rawDelta, blend);
        float mouseX = _smoothedDelta.x;
        float mouseY = _smoothedDelta.y;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -85f, 85f);

        // 카메라(이 오브젝트): 상하 회전
        transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        // 플레이어 바디: 좌우 회전
        playerBody.Rotate(Vector3.up * mouseX);
    }

    void LockCursor(bool locked)
    {
        _locked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
