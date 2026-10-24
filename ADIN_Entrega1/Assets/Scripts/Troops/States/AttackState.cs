using FSM.Enemy;
using UnityEngine;

public class AttackState : States<TroopController>, ITroopAttack
{
    [Header("Configuración del Warrior")]
    [SerializeField] private bool alternateAttackAnimations = false;
    [SerializeField] private bool useDirectionalAttackAnimation = false;
    [SerializeField] private float animationFallbackTime = 1f;

    [Header("Debug del cooldown")]
    [SerializeField] private bool debugCooldown = true;
    [SerializeField] private float debugInterval = 0.25f;

    private float nextAttackTime;
    private float attackFinishTime;
    private float nextCooldownDebugTime;
    private HealthSystem targetHealth;
    private bool useSecondAttack;
    private bool waitingForHit;
    private bool damageAppliedThisAttack;

    public bool IsAttackInProgress => waitingForHit;

    public override void OnEnter()
    {
        _controller.HurtStarted -= HandleHurtStarted;
        _controller.HurtStarted += HandleHurtStarted;
        _controller.HurtFinished -= HandleHurtFinished;
        _controller.HurtFinished += HandleHurtFinished;
        _controller.AttackAnimationFinished -= FinishAttackAnimation;
        _controller.AttackAnimationFinished += FinishAttackAnimation;

        waitingForHit = false;
        damageAppliedThisAttack = false;
        useSecondAttack = false;
        targetHealth = null;
        StopMovement();
        _controller.SetMovementSpeed(0f);
        SetAttackAnimation(false, Vector2.zero);
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null || _controller.IsHurt) return;

        if (!TryGetValidTarget(out Transform target))
        {
            ExitAttackState();
            return;
        }

        float distance = Vector2.Distance(_controller.transform.position, target.position);

        if (distance > _controller.Stats.attackRange)
        {
            waitingForHit = false;
            damageAppliedThisAttack = false;
            SetAttackAnimation(false, Vector2.zero);
            _controller.SetState(_controller.WalkState);
            return;
        }

        StopMovement();
        _controller.SetMovementSpeed(0f);
        if (_controller.Mover != null) _controller.Mover.FaceTowards(target.position);

        if (waitingForHit && Time.time >= attackFinishTime)
        {
            if (debugCooldown) Debug.Log($"[{_controller.name}] DEBUG: se fuerza el fin de la animación de ataque.", _controller);
            FinishAttackAnimation();
        }

        if (waitingForHit) return;

        if (Time.time < nextAttackTime)
        {
            if (debugCooldown && Time.time >= nextCooldownDebugTime)
            {
                Debug.Log($"[{_controller.name}] DEBUG: cooldown restante = {nextAttackTime - Time.time:F2} s", _controller);
                nextCooldownDebugTime = Time.time + Mathf.Max(0.05f, debugInterval);
            }
            return;
        }

        StartAttack(target);
    }

    private void HandleHurtStarted()
    {
        InterruptAttackForHurt();
    }

    private void HandleHurtFinished()
    {
        // El ataque interrumpido no tiene que esperar el cooldown anterior.
        nextAttackTime = Time.time;
        attackFinishTime = 0f;
        if (debugCooldown) Debug.Log($"[{_controller.name}] DEBUG: terminó Hurt; puede volver a atacar.", _controller);
    }

    private bool TryGetValidTarget(out Transform target)
    {
        target = _controller.Target;
        if (target == null)
        {
            targetHealth = null;
            return false;
        }

        HealthSystem health = target.GetComponentInParent<HealthSystem>();
        if (health == null || health.IsDead)
        {
            targetHealth = null;
            return false;
        }

        targetHealth = health;
        return true;
    }

    private void StartAttack(Transform target)
    {
        if (_controller.IsHurt || waitingForHit || targetHealth == null || targetHealth.IsDead) return;

        waitingForHit = true;
        damageAppliedThisAttack = false;
        attackFinishTime = Time.time + Mathf.Max(0.1f, animationFallbackTime);

        if (alternateAttackAnimations) useSecondAttack = !useSecondAttack;

        Vector2 direction = useDirectionalAttackAnimation ? CalculateAttackDirection(target) : Vector2.zero;
        SetAttackAnimation(true, direction);

        if (debugCooldown) Debug.Log($"[{_controller.name}] DEBUG: empieza ataque a las {Time.time:F2}; cooldown configurado = {_controller.Stats.attackCooldown:F2} s", _controller);
    }

    public void InterruptAttackForHurt()
    {
        bool wasAttacking = waitingForHit;
        waitingForHit = false;
        damageAppliedThisAttack = false;
        attackFinishTime = 0f;
        SetAttackAnimation(false, Vector2.zero);
        StopMovement();
        _controller.SetMovementSpeed(0f);

        if (debugCooldown && wasAttacking) Debug.Log($"[{_controller.name}] DEBUG: ataque interrumpido por daño.", _controller);
    }

    private void StopMovement()
    {
        if (_controller.Mover != null) _controller.Mover.Stop();
    }

    private void SetAttackAnimation(bool attacking, Vector2 direction)
    {
        _controller.SetMeleeAttackAnimation(attacking, attacking && alternateAttackAnimations && useSecondAttack, direction);
    }

    private Vector2 CalculateAttackDirection(Transform target)
    {
        Vector2 direction = (Vector2)(target.position - _controller.transform.position);
        if (direction.sqrMagnitude <= 0.001f) return Vector2.down;

        direction.Normalize();
        return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? new Vector2(Mathf.Sign(direction.x), 0f)
            : new Vector2(0f, Mathf.Sign(direction.y));
    }

    public void ApplyAttackDamage() { }

    public bool ApplyAttackDamageTo(HealthSystem health)
    {
        if (!waitingForHit || _controller.IsHurt || damageAppliedThisAttack || health == null || health.IsDead || _controller.Stats == null || targetHealth == null || targetHealth.IsDead || health != targetHealth) return false;

        damageAppliedThisAttack = true;
        health.TakeDamage(_controller.Stats.attackDamage);

        if (debugCooldown) Debug.Log($"[{_controller.name}] DEBUG: impacto aplicado. Daño = {_controller.Stats.attackDamage}", _controller);
        return true;
    }

    public void FinishAttackAnimation()
    {
        if (!waitingForHit || _controller.IsHurt) return;

        waitingForHit = false;
        SetAttackAnimation(false, Vector2.zero);

        float cooldown = Mathf.Max(0f, _controller.Stats != null ? _controller.Stats.attackCooldown : 0f);
        nextAttackTime = Time.time + cooldown;
        nextCooldownDebugTime = Time.time;

        if (debugCooldown) Debug.Log($"[{_controller.name}] DEBUG: ataque terminado; cooldown = {cooldown:F2} s.", _controller);
    }

    private void ExitAttackState()
    {
        waitingForHit = false;
        damageAppliedThisAttack = false;
        targetHealth = null;
        _controller.Target = null;
        StopMovement();
        _controller.SetMovementSpeed(0f);
        SetAttackAnimation(false, Vector2.zero);

        if (_controller.WalkState != null) _controller.SetState(_controller.WalkState);
        else if (_controller.IdleState != null) _controller.SetState(_controller.IdleState);
    }

    public override void OnExit()
    {
        _controller.HurtStarted -= HandleHurtStarted;
        _controller.HurtFinished -= HandleHurtFinished;
        _controller.AttackAnimationFinished -= FinishAttackAnimation;

        waitingForHit = false;
        damageAppliedThisAttack = false;
        StopMovement();
        _controller.SetMovementSpeed(0f);
        SetAttackAnimation(false, Vector2.zero);
    }

    public void OnAttack(Transform target) { }
}