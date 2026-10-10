using FSM.Enemy;
using UnityEngine;

public class IdleState : States<TroopController>
{
    public override void OnEnter()
    {
        Debug.Log("OnEnter IdleState");
    }

    public override void OnUpdate()
    {
        // La tropa permanece quieta.
    }

    public override void OnExit()
    {
        Debug.Log("OnExit IdleState");
    }
}
