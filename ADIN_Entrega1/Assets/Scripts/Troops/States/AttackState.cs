using FSM.Enemy;
using UnityEngine;

public class AttackState : States<TroopController>, ITroopAttack
{
    [SerializeField] private float repathInterval = 0.3f;

    [Header("Animaciones de ataque")]
    [SerializeField] private bool alternateAttackAnimations;
    [SerializeField] private bool useDirectionalAttackAnimation;

    private float nextAttack;
    private float nextRepath;
    private HealthSystem targetHealth;
    private bool useSecondAttack;
    private bool waitingForHit;

    public override void OnEnter()
    {
        targetHealth = _controller.Target != null ? _controller.Target.GetComponentInParent<HealthSystem>() : null;

        nextRepath = 0f;
        nextAttack = 0f;
        useSecondAttack = false;
        waitingForHit = false;

        _controller.SetMeleeAttackAnimation(false, false, Vector2.zero);
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null)
            return;

        Transform target = _controller.Target;

        if (target == null || targetHealth == null || targetHealth.IsDead)
        {
            waitingForHit = false;
            _controller.SetMeleeAttackAnimation(false, false, Vector2.zero);
            _controller.Target = null;

            if (_controller.WalkState != null)
                _controller.SetState(_controller.WalkState);
            else if (_controller.IdleState != null)
                _controller.SetState(_controller.IdleState);

            return;
        }

        float distance = Vector2.Distance(_controller.transform.position, target.position);

        if (distance > _controller.Stats.attackRange)
        {
            waitingForHit = false;
            _controller.SetMeleeAttackAnimation(false, false, Vector2.zero);

            if (Time.time >= nextRepath && _controller.Mover != null)
            {
                nextRepath = Time.time + repathInterval;
                _controller.Mover.MoveTo(target.position);
            }

            return;
        }

        if (_controller.Mover != null)
        {
            _controller.Mover.Stop();
            _controller.Mover.FaceTowards(target.position);
        }

        // Mientras se reproduce un ataque, no iniciar otro.
        if (waitingForHit)
            return;

        if (Time.time >= nextAttack)
        {
            nextAttack = Time.time + _controller.Stats.attackCooldown;
            waitingForHit = true;

            Vector2 direction =
                (target.position - _controller.transform.position).normalized;

            if (useDirectionalAttackAnimation)
                direction = GetCardinalDirection(direction);

            if (alternateAttackAnimations)
                useSecondAttack = !useSecondAttack;

            _controller.SetMeleeAttackAnimation(
                true,
                alternateAttackAnimations && useSecondAttack,
                direction
            );
        }
    }

    // Este método se llamará desde un Animation Event en el momento del golpe.
    public void ApplyAttackDamage()
    {
        Transform target = _controller.Target;

        if (target == null || _controller.Stats == null)
        {
            waitingForHit = false;
            return;
        }

        HealthSystem health = target.GetComponentInParent<HealthSystem>();

        if (health == null || health.IsDead)
        {
            waitingForHit = false;
            return;
        }

        float distance = Vector2.Distance(_controller.transform.position, target.position);

        // Comprobar el alcance otra vez justo en el momento del impacto.
        if (distance <= _controller.Stats.attackRange)
            health.TakeDamage(_controller.Stats.attackDamage);

        waitingForHit = false;
        _controller.SetMeleeAttackAnimation(false, false, Vector2.zero);
    }

    public override void OnExit()
    {
        waitingForHit = false;

        if (_controller.Mover != null)
            _controller.Mover.Stop();

        _controller.SetMeleeAttackAnimation(false, false, Vector2.zero);
    }

    public void OnAttack(Transform target)
    {
        // El daño se aplica mediante ApplyAttackDamage desde la animación.
    }

    private Vector2 GetCardinalDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return new Vector2(Mathf.Sign(direction.x), 0f);

        return new Vector2(0f, Mathf.Sign(direction.y));
    }
}