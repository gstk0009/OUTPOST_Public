using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(AudioSource))]
public class FootstepManager : MonoBehaviour
{
    [Header("Footstep Clips")]
    [FormerlySerializedAs("concreteSteps")]
    public AudioClip[] footstepClips;

    [Header("Intervals")]
    public float walkInterval = 0.5f;   // 기본 이동 발소리 간격
    public float sprintInterval = 0.34f;

    [Header("Volume")]
    public float walkVolume = 1.0f;     // 기본 이동 발소리 볼륨
    public float sprintVolume = 1.15f;
    // Alt 정밀 걷기와 Ctrl 앉기는 잠입 상태이므로 발소리를 생략한다.

    private AudioSource _audioSource;
    private PlayerController _player;
    private float _stepTimer;
    private bool _wasGrounded;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _player = GetComponentInParent<PlayerController>();
    }

    void Update()
    {
        // 착지 소리
        if (!_wasGrounded && _player.isGrounded)
        {
            _player.OnLanded();
            PlayLandSound();
        }
        _wasGrounded = _player.isGrounded;

        if (!_player.IsMoving || !_player.isGrounded) return;

        // Alt 정밀 걷기 / Ctrl 앉기 상태에서는 발소리 없음
        if (_player.isSneaking || _player.isCrouching)
        {
            _stepTimer = 0f;
            return;
        }

        float interval = _player.isSprinting ? sprintInterval : walkInterval;
        float volume = _player.isSprinting ? sprintVolume : walkVolume;
        _stepTimer += Time.deltaTime;
        if (_stepTimer >= interval)
        {
            PlayFootstep(volume);
            _stepTimer = 0f;
        }
    }

    void PlayFootstep(float volume)
    {
        AudioClip[] clips = GetFootstepClips();
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        _audioSource.PlayOneShot(clip, volume);
    }

    void PlayLandSound()
    {
        AudioClip[] clips = GetFootstepClips();
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        _audioSource.PlayOneShot(clip, walkVolume * 1.5f);
    }

    AudioClip[] GetFootstepClips()
    {
        return footstepClips;
    }
}
