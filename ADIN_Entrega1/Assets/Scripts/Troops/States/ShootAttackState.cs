using FSM.Enemy;
using UnityEngine;

public class ShootAttackState : States<TroopController>, ITroopAttack
{
    [Header("Disparo")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float arrowSpeed = 10f;

    [Header("Torres")]
    [SerializeField] private float structureRangeBonus = 0.75f;
    [SerializeField] private float repathInterval = 0.4f;

    private float nextAttackTime;
    private float nextRepath;

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

        bool structure = AttackState.IsStructure(target);
        float distance = structure
            ? AttackState.DistanceToTarget(_controller.transform.position, target)
            : Vector2.Distance(_controller.transform.position, target.position);
        float range = _controller.Stats.attackRange + (structure ? structureRangeBonus : 0f);

        if (distance > range)
        {
            _controller.SetAttacking(false);

            // Torre: el arquero tiene que acercarse hasta tenerla a tiro.
            if (structure && _controller.Mover != null && Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                _controller.Mover.MoveTo(AttackState.GetBodyCenter(target));
                _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
            }
            return;
        }

        if (structure && _controller.Mover != null)
        {
            _controller.Mover.Stop();
            _controller.SetMovementSpeed(0f);
        }

        if (_controller.Mover != null) _controller.Mover.FaceTowards(target.position);

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
        // Torres: se apunta al centro de su collider (el pivote queda por encima y la flecha pasaría de largo).
        Vector2 destination = AttackState.IsStructure(target) ? AttackState.GetBodyCenter(target) : (Vector2)target.position;
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
