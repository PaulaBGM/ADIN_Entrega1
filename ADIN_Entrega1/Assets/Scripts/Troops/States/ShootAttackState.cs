using FSM.Enemy;
using UnityEngine;

public class ShootAttackState : States<TroopController>, ITroopAttack
{
    public override void OnEnter()
    {
        Debug.Log("OnEnter ShootAttackState");
    }

    public override void OnUpdate()
    {
    }

    public override void OnExit()
    {
        Debug.Log("OnExit ShootAttackState");
    }

    public void OnAttack(Transform target)
    {
        //Ataca
    }
}
