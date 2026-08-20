using UnityEngine;

public class AmmoPickup : MonoBehaviour
{
    public enum AmmoKind { Rifle, Sniper, Pistol, All }

    public AmmoKind ammoKind = AmmoKind.All;
    [Min(1)] public int magazines = 1;
    public float rotateSpeed = 48f;
    [Min(0f)] public float hoverAmplitude = 0.11f;
    [Min(0f)] public float hoverSpeed = 2.25f;
    public AudioClip pickupSound;
    public event System.Action<AmmoPickup> Collected;

    Vector3 spawnPosition;
    float hoverPhase;

    void OnEnable()
    {
        spawnPosition = transform.position;
        hoverPhase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = spawnPosition + Vector3.up * (Mathf.Sin(Time.time * hoverSpeed + hoverPhase) * hoverAmplitude);
    }

    void OnTriggerEnter(Collider other)
    {
        WeaponManager manager = other.GetComponentInParent<WeaponManager>();
        if (manager == null) return;

        int added = manager.AddAmmoMagazines(ammoKind, magazines);
        if (added <= 0) return;

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.65f);
        Collected?.Invoke(this);
    }
}
