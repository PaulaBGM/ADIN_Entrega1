
using FSM.Enemy;
using UnityEngine;

public class WalkState : States<TroopController>
{
    [Header("Configuración")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField, Min(0.01f)] private float checkInterval = 0.25f;

    private float nextCheck;

    public override void OnEnter()
    {
        nextCheck = 0f;

        if (_controller.Stats == null)
        {
            Debug.LogError($"[{name}] No tiene estadísticas asignadas.", _controller);
            return;
        }

        if (_controller.Waypoint == null)
        {
            Debug.LogWarning($"[{name}] No tiene Waypoint asignado.", _controller);
            return;
        }

        bool ok = _controller.Mover.MoveTo(_controller.Waypoint.position);

        if (!ok)
        {
            Debug.LogWarning($"[{name}] No se pudo calcular el camino hasta " + $"{_controller.Waypoint.position}.", _controller);
            return;
        }

        _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null)
            return;

        // 1. Buscar enemigos según las estadísticas de la tropa.
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;

            Transform closest = FindClosestEnemy();

            if (closest != null)
            {
                _controller.Target = closest;

                if (_controller.AttackState != null)
                    _controller.SetState(_controller.AttackState);

                return;
            }
        }

        // 2. Si ha llegado a la base enemiga, atacarla.
        if (_controller.Waypoint != null && !_controller.Mover.IsMoving)
        {
            _controller.Target = _controller.Waypoint;

            if (_controller.AttackState != null)
                _controller.SetState(_controller.AttackState);

            return;
        }

        // 3. Si no está cazando y ha terminado de moverse,pasar al estado Idle.
        if (!_controller.Stats.huntEnemiesAnywhere && !_controller.Mover.IsMoving && _controller.IdleState != null)
        {
            _controller.SetState(_controller.IdleState);
        }
    }

    public override void OnExit()
    {
        if (_controller.Mover != null)
            _controller.Mover.Stop();
    }

    private Transform FindClosestEnemy()
    {
        TroopStats stats = _controller.Stats;

        if (stats == null)
            return null;

        float radius = stats.huntEnemiesAnywhere ? stats.huntRange : stats.detectionRange;

        Collider2D[] hits = Physics2D.OverlapCircleAll(_controller.transform.position, radius, enemyLayer);

        Transform closest = null;
        float bestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            HealthSystem health =
                hit.GetComponentInParent<HealthSystem>();

            if (health == null || health.IsDead)
                continue;

            if (health.transform == _controller.transform)
                continue;

            float distance = ((Vector2)health.transform.position - (Vector2)_controller.transform.position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = health.transform;
            }
        }

        return closest;
    }

    private void OnDrawGizmosSelected()
    {
        if (_controller == null || _controller.Stats == null)
            return;

        TroopStats stats = _controller.Stats;

        float radius = stats.huntEnemiesAnywhere ? stats.huntRange : stats.detectionRange;

        Gizmos.color = stats.huntEnemiesAnywhere ? Color.red : Color.yellow;

        Gizmos.DrawWireSphere(_controller.transform.position, radius);
    }
}
