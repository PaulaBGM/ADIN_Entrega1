
using FSM.Enemy;
using UnityEngine;

public class RangedIdleState : States<TroopController>
{
    [Header("Detección de enemigos")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float checkInterval = 0.25f;

    private float nextCheck;

    public override void OnEnter()
    {
        nextCheck = 0f;

        if (_controller.Mover != null)
            _controller.Mover.Stop();

        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null)
            return;

        if (Time.time < nextCheck)
            return;

        nextCheck = Time.time + checkInterval;

        float detectionRange = _controller.Stats.detectionRange;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            _controller.transform.position,
            detectionRange,
            enemyLayer
        );

        Transform closest = null;
        float bestDistance = float.MaxValue;

        HealthSystem ownHealth =
            _controller.GetComponentInChildren<HealthSystem>();

        foreach (Collider2D hit in hits)
        {
            HealthSystem health =
                hit.GetComponentInParent<HealthSystem>();

            if (health == null || health.IsDead || health == ownHealth)
                continue;

            float distance =
                ((Vector2)health.transform.position -
                 (Vector2)_controller.transform.position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = health.transform;
            }
        }

        // Si encuentra un enemigo, lo ataca.
        if (closest != null)
        {
            if (_controller.ShootAttackState == null)
            {
                Debug.LogError(
                    $"[{_controller.name}] Falta ShootAttackState."
                );
                return;
            }

            _controller.Target = closest;
            _controller.SetState(_controller.ShootAttackState);
            return;
        }

        // Si no hay enemigos cercanos, comprueba si queda
        // algún enemigo vivo en las capas configuradas.
        Collider2D[] allEnemies = Physics2D.OverlapCircleAll(
            _controller.transform.position,
            10000f,
            enemyLayer
        );

        bool enemiesAlive = false;

        foreach (Collider2D hit in allEnemies)
        {
            HealthSystem health =
                hit.GetComponentInParent<HealthSystem>();

            if (health != null &&
                !health.IsDead &&
                health != ownHealth)
            {
                enemiesAlive = true;
                break;
            }
        }

        // Si no quedan enemigos vivos, avanza hacia la base.
        if (!enemiesAlive)
        {
            if (_controller.WalkState != null &&
                _controller.Waypoint != null)
            {
                _controller.SetState(_controller.WalkState);
            }
            else if (_controller.WalkState == null)
            {
                Debug.LogWarning(
                    $"[{_controller.name}] Añade WalkState al arquero."
                );
            }
            else
            {
                Debug.LogWarning(
                    $"[{_controller.name}] No tiene Waypoint asignado."
                );
            }
        }
    }

    public override void OnExit()
    {
    }

    private void OnDrawGizmosSelected()
    {
        if (_controller == null || _controller.Stats == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            _controller.transform.position,
            _controller.Stats.detectionRange
        );
    }
}
