using UnityEngine;

// Contiene las estadísticas configurables de un tipo de tropa.
[CreateAssetMenu(
    fileName = "NewTroopStats",
    menuName = "Tropas/Estadísticas de tropa"
)]
public class TroopStats : ScriptableObject
{
    [Header("Vida")]
    [Min(1f)] public float maxHealth = 100f;
    [Min(0f)] public float defense = 0f;

    [Header("Movimiento")]
    [Min(0f)] public float movementSpeed = 3f;

    [Header("Detección")]
    public bool huntEnemiesAnywhere = true;
    [Min(0f)] public float huntRange = 200f;
    [Min(0f)] public float detectionRange = 5f;

    [Header("Ataque")]
    [Min(0f)] public float attackDamage = 10f;
    [Min(0f)] public float attackRange = 0.8f;
    [Min(0.01f)] public float attackCooldown = 1f;

    [Header("Curación")]
    [Min(0f)] public float healingPower = 0f;
    [Min(0f)] public float healingRange = 3f;
    [Min(0.01f)] public float healingCooldown = 2f;
}