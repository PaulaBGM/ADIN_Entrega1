using FSM.Enemy;
using UnityEngine;

public class IdleState : States<TroopController>
{
    public override void OnEnter()
    {
        if (_controller.Mover != null)
            _controller.Mover.Stop();
    }

    public override void OnUpdate()
    {
        // La tropa permanece quieta.
    }

    public override void OnExit() { }
}
