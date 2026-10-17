using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EventManager", menuName = "Scriptable Objects/EventManagerSO")]
public class EventManagerSO : ScriptableObject
{
    public event Action<float, float> OnTroopDamaged;
    public event Action<float, float> OnTroopHealed;
    public event Action<float, float> OnTroopUseArrow;
    public event Action<GameObject, float, float> OnEnemyDamaged;

    public void TroopNotifiesDamaged(float currentHealth, float maxHealth) => OnTroopDamaged?.Invoke(currentHealth, maxHealth);

    public void TroopUseArrow(float currentAmmoGun, float maxAmmoGun) => OnTroopUseArrow?.Invoke(currentAmmoGun, maxAmmoGun);

    public void HeallingTroopNotifiesHeal(float currentHealth, float maxHealth) => OnTroopHealed?.Invoke(currentHealth, maxHealth);

    public void EnemyNotifiesDamaged(GameObject enemy, float currentHealth, float maxHealth) => OnEnemyDamaged?.Invoke(enemy, currentHealth, maxHealth);
}
