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

    /// <summary>True cuando una torre/base ha caído: el combate ha terminado y nadie puede recibir más daño.</summary>
    public static bool BattleOver { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => BattleOver = false;

    [Header("Torres / bases (objetivos sin TroopController)")]
    [SerializeField] private float hideDelayWhenDestroyed = 0.8f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnTroopDied;

    private float currentHealth;
    private bool isDead;
    private TroopController troopController;

    public bool IsDead => isDead;
    public bool IsStructure => troopController == null;
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
        if (isDead || amount <= 0f || BattleOver)
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

        if (MovementSpritePool.Instance == null)
            return;

        Vector3 spawnPosition = particleSpawnPoint != null ? particleSpawnPoint.position : transform.position;

        MovementSpritePool.Instance.Spawn(prefab, spawnPosition, Quaternion.identity);
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

        if (troopController == null) StructureDestroyed();
    }

    // Una torre/base ha caído: fin del combate.
    private void StructureDestroyed()
    {
        BattleOver = true;
        Debug.Log($"[FIN] {name} ha sido destruida. Fin del combate.", this);

        foreach (TroopController troop in FindObjectsByType<TroopController>(FindObjectsSortMode.None))
            troop.EndBattle();

        StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelayWhenDestroyed);
        gameObject.SetActive(false);
    }

    /// <summary>Devuelve la vida al máximo y reactiva el objeto (para reiniciar la formación).</summary>
    public void ResetHealth()
    {
        StopAllCoroutines();
        isDead = false;
        BattleOver = false;
        gameObject.SetActive(true);
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
}
