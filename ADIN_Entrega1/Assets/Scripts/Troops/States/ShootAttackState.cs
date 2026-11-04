using FSM.Enemy;
using UnityEngine;

public class ShootAttackState : States<TroopController>, ITroopAttack
{
    [Header("Disparo")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float arrowSpeed = 10f;

    private float nextAttackTime;

    public override void OnEnter()
    {
        nextAttackTime = 0f;

        if (_controller.Mover != null)
            _controller.Mover.Stop();

        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null)
            return;

        Transform target = _controller.Target;

        if (target == null)
        {
            ReturnToIdle();
            return;
        }

        HealthSystem health = target.GetComponentInParent<HealthSystem>();

        if (health == null || health.IsDead)
        {
            ReturnToIdle();
            return;
        }

        float distance = Vector2.Distance(_controller.transform.position, target.position);

        if (distance > _controller.Stats.attackRange)
        {
            _controller.SetAttacking(false);
            return;
        }

        _controller.SetAttacking(true);

        if (Time.time >= nextAttackTime)
        {
            Debug.Log($"Disparo permitido. Distancia: {distance}, Rango: {_controller.Stats.attackRange}");

            OnAttack(target);
            nextAttackTime = Time.time + _controller.Stats.attackCooldown;
        }
    }

    public void OnAttack(Transform target)
    {
        if (target == null || arrowPrefab == null || firePoint == null)
        {
            Debug.LogWarning($"[{_controller.name}] Falta el objetivo, Arrow Prefab o Fire Point.");
            return;
        }

        if (ArrowPool.Instance == null)
        {
            Debug.LogWarning("[FLECHA] No existe ArrowPool en la escena.");
            return;
        }

        Vector2 origin = firePoint.position;
        Vector2 destination = target.position;
        Vector2 direction = (destination - origin).normalized;

        if (direction.sqrMagnitude == 0f)
            return;

        ArrowPool.Instance.Spawn(origin, direction, arrowSpeed, _controller.Stats.attackDamage, _controller.gameObject, target);
    }

    private void ReturnToIdle()
    {
        _controller.SetAttacking(false);
        _controller.Target = null;

        if (_controller.RangedIdleState != null)
            _controller.SetState(_controller.RangedIdleState);
        else if (_controller.IdleState != null)
            _controller.SetState(_controller.IdleState);
    }

    public override void OnExit()
    {
        _controller.SetAttacking(false);
    }
}
