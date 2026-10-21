
using System;
using UnityEngine;
using FSM.Enemy;

public class HealthSystem : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnTroopDied;

    private float currentHealth;
    private bool isDead;
    private TroopController troopController;

    public bool IsDead => isDead;

    // Indica si la tropa sigue viva y necesita curación.
    public bool NeedsHealing => !isDead && currentHealth < maxHealth;

    private void Awake()
    {
        troopController = GetComponentInParent<TroopController>();

        if (troopController != null && troopController.Stats != null)
            maxHealth = troopController.Stats.maxHealth;

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        float defense = troopController != null &&
                        troopController.Stats != null
            ? troopController.Stats.defense
            : 0f;

        float finalDamage = Mathf.Max(0f, amount - defense);

        if (finalDamage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        troopController?.PlayHitAnimation();
    }

    // Recupera vida sin superar la vida máxima.
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f || currentHealth >= maxHealth)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        // Actualizar la barra de vida y otros sistemas suscritos.
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        // Activar la animación antes de notificar la muerte.
        troopController?.PlayDeathAnimation();

        OnTroopDied?.Invoke();
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}
