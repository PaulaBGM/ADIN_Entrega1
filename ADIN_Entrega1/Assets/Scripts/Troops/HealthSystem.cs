using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    public event Action<float, float> OnHealthChanged; // Para la barra de vida
    public event Action OnTroopDied; // Para que TroopController o efectos lo escuchen
    
    private float currentHealth;
    private bool isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
            
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);
            
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
            Die();
    }
    
    private void Die()
    {
        if (isDead) return;
        isDead = true;
        
        // Notifica que murió
        OnTroopDied?.Invoke();
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}
