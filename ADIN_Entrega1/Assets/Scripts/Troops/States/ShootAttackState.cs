
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

        if (_controller.Mover != null) _controller.Mover.Stop();

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

        // Esperar hasta que el enemigo entre en el rango de ataque.
        if (distance > _controller.Stats.attackRange)
        {
            _controller.SetAttacking(false);
            return;
        }

        // El enemigo ya está dentro del rango.
        _controller.SetAttacking(true);

        if (Time.time >= nextAttackTime)
        {
            Debug.Log(
                $"Disparo permitido. Distancia: {distance}, " +
                $"Rango: {_controller.Stats.attackRange}"
            );
            
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

        // Calcular la dirección desde el punto de disparo hasta el objetivo.
        Vector2 origin = firePoint.position;
        Vector2 destination = target.position;
        Vector2 direction = (destination - origin).normalized;

        if (direction.sqrMagnitude == 0f)
            return;

        // Crear la flecha orientada hacia el objetivo.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        GameObject arrow = Instantiate(arrowPrefab, origin, Quaternion.Euler(0f, 0f, angle));

        // Darle dirección, velocidad y daño.
        ArrowProjectile projectile = arrow.GetComponent<ArrowProjectile>();

        if (projectile == null)
        {
            Debug.LogError("El prefab de la flecha no tiene ArrowProjectile.");
            Destroy(arrow);
            return;
        }

        projectile.Initialize(direction, arrowSpeed, _controller.Stats.attackDamage, _controller.gameObject);
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
