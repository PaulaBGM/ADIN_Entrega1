using UnityEngine;
using FSM.Enemy;

public class TroopAnimationEvents : MonoBehaviour
{
    private TroopController troopController;

    private void Awake()
    {
        troopController = GetComponentInParent<TroopController>();
    }

    // Esta función se llama desde el Animation Event.
    public void ApplyAttackDamage()
    {
        if (troopController == null)
            return;

        if (troopController.AttackState != null)
            troopController.AttackState.ApplyAttackDamage();
    }
    
    public void ApplyHeal()
    {
        if (troopController == null || troopController.HealState == null)
            return;

        troopController.HealState.ApplyHeal();
    }

    public void FinishHeal()
    {
        if (troopController == null || troopController.HealState == null)
            return;

        troopController.HealState.FinishHeal();
    }
}