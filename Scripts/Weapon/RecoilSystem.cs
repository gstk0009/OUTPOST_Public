using UnityEngine;

/// <summary>
/// 카메라 하위에 배치. 발사 시 AddRecoil() 호출 → 카메라가 위/옆으로 튐.
/// </summary>
public class RecoilSystem : MonoBehaviour
{
    [Header("Speed")]
    public float recoilSpeed = 15f;
    public float returnSpeed = 8f;

    private Vector3 _currentRecoil;
    private Vector3 _targetRecoil;

    /// <summary>vertical: 위로 올라가는 반동(양수), horizontal: 좌우 랜덤</summary>
    public void AddRecoil(float vertical, float horizontal)
    {
        _targetRecoil += new Vector3(-vertical, horizontal, 0f);
    }

    void Update()
    {
        _currentRecoil = Vector3.Lerp(_currentRecoil, _targetRecoil, recoilSpeed * Time.deltaTime);
        _targetRecoil = Vector3.Lerp(_targetRecoil, Vector3.zero, returnSpeed * Time.deltaTime);

        transform.localRotation = Quaternion.Euler(_currentRecoil);
    }
}
