
using FSM.Enemy;
using UnityEngine;

public class RangedIdleState : States<TroopController>
{
    [Header("Detección de enemigos")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float checkInterval = 0.25f;

    private float nextCheck;
    private bool hunting;

    public override void OnEnter()
    {
        nextCheck = 0f;
        hunting = false;

        if (_controller.Mover != null)
            _controller.Mover.Stop();

        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null || !_controller.AttacksEnabled)
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

            if (health.gameObject.layer == _controller.gameObject.layer)
                continue;   // mismo bando

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
        Transform nearestEnemy = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider2D hit in allEnemies)
        {
            HealthSystem health =
                hit.GetComponentInParent<HealthSystem>();

            if (health != null &&
                !health.IsDead &&
                health != ownHealth &&
                health.gameObject.layer != _controller.gameObject.layer)
            {
                enemiesAlive = true;

                float d = ((Vector2)health.transform.position - (Vector2)_controller.transform.position).sqrMagnitude;
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearestEnemy = health.transform;
                }
            }
        }

        // Solo quedan arqueros y monjes: nadie va a venir a por ellos, así que salen a buscar al enemigo
        // más cercano. Cuando entra en detectionRange, el bloque de arriba pasa a disparar.
        if (enemiesAlive && nearestEnemy != null && TroopController.OnlyRangedAndHealersLeft())
        {
            hunting = true;

            if (_controller.Mover != null)
            {
                _controller.Mover.MoveTo(nearestEnemy.position);
                _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
            }
            return;
        }

        // Ya no hace falta salir a cazar (vuelve a haber tropas cuerpo a cuerpo): se queda a cubierto.
        if (hunting)
        {
            hunting = false;
            if (_controller.Mover != null) _controller.Mover.Stop();
            _controller.SetMovementSpeed(0f);
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
