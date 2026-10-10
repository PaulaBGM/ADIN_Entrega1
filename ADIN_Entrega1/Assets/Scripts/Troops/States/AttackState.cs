using FSM.Enemy;
using UnityEngine;

public class AttackState : States<TroopController>, ITroopAttack
{
    public override void OnEnter()
    {
        Debug.Log("OnEnter AttackState");
    }

    public override void OnUpdate()
    {
        
    }

    public override void OnExit()
    {
        Debug.Log("OnExit AttackState");
    }

    public void OnAttack(Transform target)
    {
        //Ataque tropas de melee
    }
}
