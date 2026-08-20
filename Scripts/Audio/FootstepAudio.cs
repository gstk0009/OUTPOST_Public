using UnityEngine;

/// <summary>
/// FootstepManager와 분리된 오디오 재생 헬퍼.
/// FootstepManager에서 직접 호출하거나, Animation Event에서 호출 가능.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FootstepAudio : MonoBehaviour
{
    [Header("Surface Clips")]
    public AudioClip[] concrete;
    public AudioClip[] metal;
    public AudioClip[] wood;
    public AudioClip[] gravel;

    private AudioSource _audioSource;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void PlayStep(string surfaceTag = "Concrete", float volume = 1f)
    {
        AudioClip[] clips = surfaceTag switch
        {
            "Metal"   => metal,
            "Wood"    => wood,
            "Gravel"  => gravel,
            _         => concrete
        };

        if (clips == null || clips.Length == 0) return;
        _audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }

    /// <summary>Animation Event에서 호출용 (기본 콘크리트 소리)</summary>
    public void OnFootstepEvent()
    {
        PlayStep();
    }
}
