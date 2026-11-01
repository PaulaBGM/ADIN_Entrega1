
using System;
using System.Collections;
using UnityEngine;
using FSM.Enemy;

public class HealthSystem : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    [Header("Damage Effects")]
    [SerializeField] private GameObject damageParticlesPrefab;

    [Header("Heal Effects")]
    [SerializeField] private GameObject healParticlesPrefab;

    [Header("Particle Spawn Point")]
    [SerializeField] private Transform particleSpawnPoint;

    [Header("Damage Knockback")]
    [SerializeField] private float knockbackDistance = 0.15f;
    [SerializeField] private float knockbackDuration = 0.1f;

    private Coroutine knockbackCoroutine;

    public event Action<float, float> OnHealthChanged;
    public event Action OnTroopDied;

    private float currentHealth;
    private bool isDead;
    private TroopController troopController;

    public bool IsDead => isDead;
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

        float defense = troopController != null && troopController.Stats != null ? troopController.Stats.defense : 0f;

        float finalDamage = Mathf.Max(0f, amount - defense);

        if (finalDamage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        SpawnParticles(damageParticlesPrefab);
        PlayKnockback();

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        troopController?.PlayHitAnimation();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f || currentHealth >= maxHealth)
            return;

        float previousHealth = currentHealth;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        float healedAmount = currentHealth - previousHealth;

        if (healedAmount <= 0f)
            return;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        SpawnParticles(healParticlesPrefab);

        Debug.Log($"[HEAL] {name} ha recuperado {healedAmount} de vida.");
    }

    private void SpawnParticles(GameObject prefab)
    {
        if (prefab == null)
            return;

        Vector3 spawnPosition = particleSpawnPoint != null ? particleSpawnPoint.position : transform.position;

        Instantiate(prefab, spawnPosition, Quaternion.identity);
    }

    private void PlayKnockback()
    {
        if (knockbackCoroutine != null)
            StopCoroutine(knockbackCoroutine);

        knockbackCoroutine = StartCoroutine(KnockbackCoroutine());
    }

    private IEnumerator KnockbackCoroutine()
    {
        Vector3 originalPosition = transform.position;
        Vector3 knockbackDirection = -transform.right;
        Vector3 targetPosition = originalPosition + knockbackDirection * knockbackDistance;

        float elapsed = 0f;

        while (elapsed < knockbackDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / knockbackDuration;

            transform.position = Vector3.Lerp(originalPosition, targetPosition, t);

            yield return null;
        }

        transform.position = originalPosition;
        knockbackCoroutine = null;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        troopController?.PlayDeathAnimation();

        OnTroopDied?.Invoke();
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}
