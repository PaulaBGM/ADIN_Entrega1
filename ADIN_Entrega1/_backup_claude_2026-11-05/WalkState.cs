using FSM.Enemy;
using UnityEngine;

public class WalkState : States<TroopController>
{
    [Header("Configuración")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField, Min(0.01f)] private float checkInterval = 0.25f;

    [Header("Torre enemiga")]
    [Tooltip("Opcional. Capa de las torres. Si se deja vacía, la torre enemiga es la estructura " +
             "(HealthSystem sin TroopController) más cercana al Waypoint de la tropa.")]
    [SerializeField] private LayerMask towerLayer;
    [SerializeField] private bool debugTower = true;

    [Header("Sprites al caminar")]
    [SerializeField, Min(0.05f)] private float spriteInterval = 0.4f;
    [SerializeField] private Transform spriteSpawnPoint;

    private float nextSpriteTime;

    private float nextCheck;
    private bool resumeAfterHurt;
    private HealthSystem targetHealth;   // Tropa enemiga detectada (se comprueba su distancia cada frame)
    private HealthSystem enemyTower;     // Torre enemiga (se busca una vez y se cachea)

    private float nextParticleTime;

    public override void OnEnter()
    {
        nextParticleTime = 0f;
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

        UpdateMovementSprites();

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

        // 1. Tropa enemiga ya detectada: en cuanto entra en rango de ataque, se para y ataca (cada frame).
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

        // 2. Buscar tropas enemigas (tienen prioridad sobre la torre).
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

            // 2b. Sin tropas enemigas cerca y la torre enemiga dentro del radio de detección: a por ella.
            HealthSystem tower = GetEnemyTower();
            if (tower != null && _controller.AttackState != null &&
                AttackState.DistanceToTarget(_controller.transform.position, tower.transform) <= _controller.Stats.detectionRange)
            {
                AttackTower(tower);
                return;
            }
        }

        // 3. Ha llegado al waypoint (base enemiga): atacar la TORRE, no el waypoint (que no tiene vida).
        if (_controller.Waypoint != null && !_controller.Mover.IsMoving)
        {
            HealthSystem tower = GetEnemyTower();

            if (tower != null && _controller.AttackState != null)
            {
                AttackTower(tower);
                return;
            }

            // No queda torre enemiga: quedarse en Idle en vez de reintentar cada frame.
            if (_controller.IdleState != null)
            {
                _controller.SetState(_controller.IdleState);
                return;
            }
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

    // ---------- Torre ----------

    private void AttackTower(HealthSystem tower)
    {
        _controller.Target = tower.transform;
        _controller.SetState(_controller.AttackState); // AttackState se acerca a ella si aún no está a rango

        if (debugTower)
            Debug.Log($"[{_controller.name}] Ataca la torre {tower.name}.", _controller);
    }

    private HealthSystem GetEnemyTower()
    {
        if (enemyTower != null && !enemyTower.IsDead)
            return enemyTower;

        enemyTower = FindEnemyTower();
        return enemyTower;
    }

    private HealthSystem FindEnemyTower()
    {
        HealthSystem ownHealth = _controller.GetComponent<HealthSystem>();
        Vector2 reference = _controller.Waypoint != null
            ? (Vector2)_controller.Waypoint.position
            : (Vector2)_controller.transform.position;

        HealthSystem best = null;
        float bestDistance = float.MaxValue;

        if (towerLayer.value != 0)
        {
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(_controller.transform.position, 1000f, towerLayer))
                ConsiderTower(hit.GetComponentInParent<HealthSystem>(), ownHealth, reference, ref best, ref bestDistance);
        }
        else
        {
            if (_controller.Waypoint == null) return null; // Sin waypoint ni capa no sé cuál es la enemiga

            foreach (HealthSystem h in FindObjectsByType<HealthSystem>(FindObjectsSortMode.None))
                ConsiderTower(h, ownHealth, reference, ref best, ref bestDistance);
        }

        if (best != null && debugTower)
            Debug.Log($"[{_controller.name}] Torre enemiga: {best.name}.", _controller);

        return best;
    }

    private static void ConsiderTower(HealthSystem h, HealthSystem ownHealth, Vector2 reference,
                                      ref HealthSystem best, ref float bestDistance)
    {
        if (h == null || h == ownHealth || h.IsDead) return;
        if (!AttackState.IsStructure(h.transform)) return; // solo estructuras

        float d = Vector2.Distance(reference, h.transform.position);
        if (d < bestDistance)
        {
            bestDistance = d;
            best = h;
        }
    }

    // ---------- Movimiento y detección ----------

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
        // Hasta el borde del objetivo, no hasta su centro.
        return AttackState.DistanceToTarget(_controller.transform.position, t) <= _controller.Stats.attackRange;
    }

    // Solo TROPAS enemigas: la torre se trata aparte para que las tropas tengan prioridad.
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

            if (AttackState.IsStructure(health.transform))
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

    private void UpdateMovementSprites()
    {
        if (MovementSpritePool.Instance == null)
        {
            return;
        }

        if (Time.time < nextSpriteTime)
            return;

        nextSpriteTime = Time.time + spriteInterval;

        Vector3 spawnPosition = spriteSpawnPoint != null ? spriteSpawnPoint.position : _controller.transform.position;

        MovementSpritePool.Instance.Spawn(spawnPosition);
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