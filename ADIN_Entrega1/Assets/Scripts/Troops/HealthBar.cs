using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private Image fillImage;

    private void Start()
    {
        if (healthSystem == null)
            healthSystem = GetComponentInParent<HealthSystem>();

        if (healthSystem == null)
        {
            Debug.LogError($"[{name}] No se encontró HealthSystem.");
            enabled = false;
            return;
        }

        healthSystem.OnHealthChanged += UpdateHealthBar;

        // Mostrar la vida inicial
        UpdateHealthBar(
            healthSystem.GetCurrentHealth(),
            healthSystem.GetMaxHealth()
        );
    }

    private void OnDestroy()
    {
        if (healthSystem != null)
            healthSystem.OnHealthChanged -= UpdateHealthBar;
    }

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (fillImage == null || maxHealth <= 0f)
            return;

        fillImage.fillAmount = currentHealth / maxHealth;
    }
}
