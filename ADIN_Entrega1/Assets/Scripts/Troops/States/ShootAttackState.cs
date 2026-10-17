using FSM.Enemy;
using UnityEngine;

public class ShootAttackState : States<TroopController>, ITroopAttack
{
    public override void OnEnter() { }

    public override void OnUpdate()
    {
        // Pendiente: lógica de tropas a distancia
    }

    public override void OnExit() { }

    public void OnAttack(Transform target)
    {
        // Pendiente: disparo
    }
}
