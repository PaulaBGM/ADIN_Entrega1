using FSM.Enemy;
using UnityEngine;

public class WalkState : States<TroopController>
{
    public override void OnEnter()
    {
        Debug.Log("OnEnter WalkState");
    }

    public override void OnUpdate()
    {
        // Lógica de movimiento de la tropa.
    }

    public override void OnExit()
    {
        Debug.Log("OnExit WalkState");
    }
}
