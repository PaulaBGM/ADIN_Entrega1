using FSM.Enemy;
using UnityEngine;

public class WalkState : States<TroopController>
{
    [Header("Configuración")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField, Min(0.01f)] private float checkInterval = 0.25f;

    private float nextCheck;
    private bool resumeAfterHurt;
    private HealthSystem targetHealth;   // Enemigo detectado (se comprueba su distancia cada frame)

    public override void OnEnter()
    {
        nextCheck = 0f;
        resumeAfterHurt = false;
        targetHealth = null;

        if (_controller.Stats == null)
        {
            Debug.LogError($"[{name}] No tiene estadísticas asignadas.", _controller);
            return;
        }

        if (_controller.IsHurt)
        {
            if (_controller.Mover != null) _controller.Mover.Stop();
            _controller.SetMovementSpeed(0f);
            resumeAfterHurt = true;   // Al acabar el hurt, retoma el camino
            return;
        }

        MoveToWaypoint();
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null)
            return;

        // 0. Golpeada: se queda quieta hasta que acabe la animación de hurt.
        if (_controller.IsHurt)
        {
            if (_controller.Mover != null) _controller.Mover.Stop();
            resumeAfterHurt = true;
            return;
        }

        if (resumeAfterHurt)
        {
            resumeAfterHurt = false;
            nextCheck = 0f; // Buscar enemigos ya

            if (targetHealth != null && !targetHealth.IsDead)
            {
                _controller.Mover.MoveTo(targetHealth.transform.position);
                _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
            }
            else MoveToWaypoint();
        }

        // 1. Enemigo ya detectado: en cuanto entra en rango de ataque, se para y ataca (cada frame).
        if (targetHealth != null)
        {
            if (targetHealth.IsDead) targetHealth = null;
            else if (InAttackRange(targetHealth.transform) && _controller.AttackState != null)
            {
                _controller.Target = targetHealth.transform;
                _controller.SetState(_controller.AttackState);
                return;
            }
        }

        // 2. Buscar enemigos según las estadísticas de la tropa.
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;

            HealthSystem closest = FindClosestEnemy();

            if (closest != null)
            {
                targetHealth = closest;
                _controller.Target = closest.transform;

                if (InAttackRange(closest.transform) && _controller.AttackState != null)
                {
                    _controller.SetState(_controller.AttackState);
                    return;
                }

                if (_controller.Mover != null)
                {
                    _controller.Mover.MoveTo(closest.transform.position);
                    _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
                }

                return;
            }

            // Había enemigo y ya no (murió o salió del radio): volver al camino hacia la base rival.
            if (targetHealth != null)
            {
                targetHealth = null;
                MoveToWaypoint();
            }
        }

        // 3. Si ha llegado a la base enemiga, atacarla.
        if (_controller.Waypoint != null && !_controller.Mover.IsMoving)
        {
            _controller.Target = _controller.Waypoint;

            if (_controller.AttackState != null)
                _controller.SetState(_controller.AttackState);

            return;
        }

        // 4. Si no está cazando y ha terminado de moverse, pasar al estado Idle.
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

    private bool MoveToWaypoint()
    {
        if (_controller.Waypoint == null)
        {
            Debug.LogWarning($"[{name}] No tiene Waypoint asignado.", _controller);
            return false;
        }

        if (!_controller.Mover.MoveTo(_controller.Waypoint.position))
        {
            Debug.LogWarning($"[{name}] No se pudo calcular el camino hasta {_controller.Waypoint.position}.", _controller);
            return false;
        }

        _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
        return true;
    }

    private bool InAttackRange(Transform t)
    {
        return Vector2.Distance(_controller.transform.position, t.position) <= _controller.Stats.attackRange;
    }

    private HealthSystem FindClosestEnemy()
    {
        TroopStats stats = _controller.Stats;

        if (stats == null)
            return null;

        float radius = stats.huntEnemiesAnywhere ? stats.huntRange : stats.detectionRange;

        Collider2D[] hits = Physics2D.OverlapCircleAll(_controller.transform.position, radius, enemyLayer);

        HealthSystem closest = null;
        float bestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            HealthSystem health = hit.GetComponentInParent<HealthSystem>();

            if (health == null || health.IsDead)
                continue;

            if (health.transform == _controller.transform)
                continue;

            float distance = ((Vector2)health.transform.position - (Vector2)_controller.transform.position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = health;
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