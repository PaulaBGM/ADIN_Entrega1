using FSM.Enemy;
using UnityEngine;

public class WalkState : States<TroopController>
{
    [Header("Búsqueda de enemigos")]
    [SerializeField, Tooltip("Si está activo, la tropa busca al enemigo más cercano de TODO el mapa (el waypoint solo se usa si no hay ninguno).")]
    private bool huntEnemiesAnywhere = true;
    [SerializeField, Tooltip("Radio de búsqueda cuando huntEnemiesAnywhere está activo.")]
    private float huntRange = 200f;
    [SerializeField, Tooltip("Radio de detección cuando huntEnemiesAnywhere está desactivado (va al waypoint y ataca a quien se cruce).")]
    private float detectionRange = 5f;
    [SerializeField] private LayerMask enemyLayer;        // Capa de las tropas del equipo contrario
    [SerializeField] private float checkInterval = 0.25f; // Cada cuánto busca enemigos

    private float nextCheck;

    public override void OnEnter()
    {
        nextCheck = 0f; // Buscar enemigos desde el primer frame

        if (_controller.Waypoint == null)
        {
            Debug.LogWarning($"[{name}] No tiene Waypoint asignado.");
            return;
        }
        
        bool ok = _controller.Mover.MoveTo(_controller.Waypoint.position);
        if (!ok)
        {
            Debug.LogWarning($"[{name}] No se pudo calcular un camino desde {transform.position} hasta " +
                             $"{_controller.Waypoint.position}. Revisa que el grid esté construido y que " +
                             $"la tropa y el waypoint estén sobre celdas transitables.");
        }
        else
        {
            _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
        }
    }

    
    public override void OnUpdate()
    {
        // 1) Buscar enemigos y atacarlos.
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;

            Transform closest = FindClosestEnemy();

            if (closest != null)
            {
                _controller.Target = closest;
                _controller.SetState(_controller.AttackState);
                return;
            }
        }

        // 2) Si ha llegado a la torre enemiga, atacarla.
        if (_controller.Waypoint != null && !_controller.Mover.IsMoving)
        {
            _controller.Target = _controller.Waypoint;
            _controller.SetState(_controller.AttackState);
            return;
        }

        // 3) Si no está cazando y ha terminado de moverse,
        // pasar al estado Idle.
        if (!huntEnemiesAnywhere && !_controller.Mover.IsMoving && _controller.IdleState != null)
        {
            _controller.SetState(_controller.IdleState);
        }
    }

    public override void OnExit()
    {
        _controller.Mover.Stop();
    }

    private Transform FindClosestEnemy()
    {
        float radius = huntEnemiesAnywhere ? huntRange : detectionRange;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, enemyLayer);

        Transform closest = null;
        float best = float.MaxValue;

        foreach (var hit in hits)
        {
            var health = hit.GetComponentInParent<HealthSystem>();
            if (health == null || health.IsDead) continue;

            float d = ((Vector2)health.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                closest = health.transform;
            }
        }

        return closest;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_controller == null) return;

        float radius = huntEnemiesAnywhere ? huntRange : detectionRange;

        Gizmos.color = huntEnemiesAnywhere ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(_controller.transform.position, radius);
    }
}

