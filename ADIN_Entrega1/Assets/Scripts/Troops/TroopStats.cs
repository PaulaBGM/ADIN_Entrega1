using UnityEngine;

// Contiene las estadísticas configurables de un tipo de tropa.
// Se pueden crear diferentes assets desde el menú de Unity.
[CreateAssetMenu(fileName = "NewTroopStats", menuName = "Tropas/Estadísticas de tropa")]
public class TroopStats : ScriptableObject
{
    [Header("Vida")]
    [Min(1f)] public float maxHealth = 100f;

    [Header("Movimiento")]
    [Min(0f)] public float movementSpeed = 3f;

    [Header("Ataque")]
    [Min(0f)] public float attackDamage = 10f;
    [Min(0f)] public float attackRange = 0.8f;
    [Min(0.01f)] public float attackCooldown = 1f;

    [Header("Defensa")]
    [Min(0f)] public float defense = 0f;

    [Header("Curación")]
    [Min(0f)] public float healingPower = 0f;
    [Min(0f)] public float healingRange = 3f;
    [Min(0.01f)] public float healingCooldown = 2f;

    [Header("Detección")]
    [Min(0f)] public float detectionRange = 5f;
}