using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    [Min(1f)] public float healAmount = 35f;
    public float rotateSpeed = 24f;
    [Min(0f)] public float hoverAmplitude = 0.1f;
    [Min(0f)] public float hoverSpeed = 2f;
    public AudioClip pickupSound;
    public event System.Action<HealthPickup> Collected;

    Vector3 _spawnPosition;
    float _hoverPhase;

    void OnEnable()
    {
        _spawnPosition = transform.position;
        _hoverPhase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = _spawnPosition + Vector3.up * (Mathf.Sin(Time.time * hoverSpeed + _hoverPhase) * hoverAmplitude);
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.Heal(healAmount) <= 0f) return;
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.65f);
        Collected?.Invoke(this);
    }
}
