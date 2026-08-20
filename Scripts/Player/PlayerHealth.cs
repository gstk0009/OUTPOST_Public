using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private HUDManager _hud;
    private PlayerDamageFeedback _damageFeedback;

    void Awake()
    {
        CurrentHealth = maxHealth;
    }

    void Start()
    {
        _hud = FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
        _damageFeedback = FindFirstObjectByType<PlayerDamageFeedback>(FindObjectsInactive.Include);
        _hud?.UpdateHealth(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        _hud?.UpdateHealth(CurrentHealth, maxHealth);
        _damageFeedback?.Play(damage);
        if (CurrentHealth <= 0f)
            Die();
    }

    public float Heal(float amount)
    {
        if (IsDead || amount <= 0f || CurrentHealth >= maxHealth)
            return 0f;

        float before = CurrentHealth;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        _hud?.UpdateHealth(CurrentHealth, maxHealth);
        return CurrentHealth - before;
    }

    void Die()
    {
        IsDead = true;
        PlayerController movement = GetComponent<PlayerController>();
        WeaponManager weapons = GetComponent<WeaponManager>();
        if (movement != null) movement.enabled = false;
        if (weapons != null) weapons.enabled = false;
        Debug.Log("[PlayerHealth] Player died.");
    }
}
