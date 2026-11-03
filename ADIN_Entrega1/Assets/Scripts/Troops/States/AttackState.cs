using FSM.Enemy;
using UnityEngine;

public class AttackState : States<TroopController>, ITroopAttack
{
    [Header("Configuración del Warrior")]
    [SerializeField] private bool alternateAttackAnimations = false;
    [SerializeField] private bool useDirectionalAttackAnimation = false;
    [SerializeField] private float animationFallbackTime = 1f;

    [Header("Dirección del ataque (Lancero)")]
    [Tooltip("8 direcciones (incluye diagonales). Si está desactivado, usa 4 direcciones.")]
    [SerializeField] private bool useEightDirections = true;
    [Tooltip("Actívalo si el Blend Tree solo tiene clips mirando a la DERECHA y el lado izquierdo se consigue volteando el sprite (FaceTowards). " +
             "Desactívalo si el Blend Tree tiene clips propios para la izquierda (DirectionX negativo).")]
    [SerializeField] private bool mirrorLeftWithSpriteFlip = true;
    [Tooltip("Solo en modo 4 direcciones: cuánto tiene que dominar el eje vertical para elegir arriba/abajo.")]
    [SerializeField] private float verticalBias = 1.2f;
    [Tooltip("Grados extra que tiene que girar el objetivo para cambiar de dirección respecto al ataque anterior. " +
             "Evita que el lancero alterne entre dos direcciones cuando el enemigo está justo en la frontera (p. ej. entre derecha y arriba-derecha).")]
    [SerializeField] private float directionHysteresis = 12f;
    [SerializeField] private string directionXParam = "DirectionX";
    [SerializeField] private string directionYParam = "DirectionY";

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

    private Animator animator;
    private TroopAnimationEvents animationEvents;
    private Vector2 currentAttackDirection;

    public bool IsAttackInProgress => waitingForHit;

    /// <summary>True mientras este estado es el activo de la FSM.</summary>
    public bool IsActive { get; private set; }

    [Header("Estructuras (torres)")]
    [SerializeField] private float structureRepathInterval = 0.4f;
    private float nextStructureRepath;

    public override void OnEnter()
    {
        IsActive = true;
        nextStructureRepath = 0f;
        _controller.HurtStarted -= HandleHurtStarted;
        _controller.HurtStarted += HandleHurtStarted;
        _controller.HurtFinished -= HandleHurtFinished;
        _controller.HurtFinished += HandleHurtFinished;
        _controller.AttackAnimationFinished -= FinishAttackAnimation;
        _controller.AttackAnimationFinished += FinishAttackAnimation;

        animator = _controller.GetComponentInChildren<Animator>();
        animationEvents = _controller.GetComponentInChildren<TroopAnimationEvents>();

        waitingForHit = false;
        damageAppliedThisAttack = false;
        useSecondAttack = false;
        targetHealth = null;
        currentAttackDirection = Vector2.zero;
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

        // Distancia hasta el BORDE del objetivo, no hasta su centro: con objetivos grandes
        // (torres) el centro puede estar lejos aunque la tropa ya esté pegada a la pared.
        float distance = DistanceToTarget(_controller.transform.position, target);

        if (distance > _controller.Stats.attackRange && IsStructure(target) && !waitingForHit)
        {
            // Torre: WalkState no sabe acercarse a ella, así que el propio estado
            // camina hasta el punto más cercano de su collider.
            if (Time.time >= nextStructureRepath && _controller.Mover != null)
            {
                nextStructureRepath = Time.time + structureRepathInterval;
                Collider2D body = GetBodyCollider(target);
                Vector2 point = body != null ? body.ClosestPoint(_controller.transform.position) : (Vector2)target.position;
                _controller.Mover.MoveTo(point);
                _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
            }
            return;
        }

        if (distance > _controller.Stats.attackRange)
        {
            // Si se sale a mitad de un ataque, el cooldown también cuenta.
            if (waitingForHit) StartCooldown();
            waitingForHit = false;
            damageAppliedThisAttack = false;
            SetAttackAnimation(false, Vector2.zero);
            _controller.SetState(_controller.WalkState);
            return;
        }

        StopMovement();
        _controller.SetMovementSpeed(0f);

        if (waitingForHit && Time.time >= attackFinishTime)
        {
            if (debugCooldown) Debug.Log($"[{_controller.name}] DEBUG: se fuerza el fin de la animación de ataque.", _controller);
            FinishAttackAnimation();
        }

        if (waitingForHit)
        {
            // Durante el golpe se mantiene la dirección fijada al empezar, por si otro script
            // (movimiento, idle...) sobrescribe DirectionX/DirectionY en el Animator.
            ApplyDirectionToAnimator(currentAttackDirection);
            return;
        }

        // Solo se gira hacia el objetivo cuando NO está atacando, para no voltear el sprite a mitad del golpe.
        if (_controller.Mover != null) _controller.Mover.FaceTowards(target.position);

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

        currentAttackDirection = useDirectionalAttackAnimation ? CalculateAttackDirection(target) : Vector2.zero;

        // Si el Blend Tree tiene clips propios de izquierda, el sprite no se puede voltear:
        // el clip ya mira a la izquierda y se vería volteado dos veces (lanza hacia la derecha).
        // Se hace ANTES de orientar la hitbox, porque la hitbox mira la escala del Visual.
        if (useDirectionalAttackAnimation && !mirrorLeftWithSpriteFlip && _controller.Mover != null)
            _controller.Mover.ResetFacing();

        // Primero la dirección y la hitbox, después se activa el ataque,
        // para que el Blend Tree arranque ya en el clip correcto.
        ApplyDirectionToAnimator(currentAttackDirection);
        if (animationEvents != null && useDirectionalAttackAnimation)
            animationEvents.SetAttackDirection(currentAttackDirection);

        SetAttackAnimation(true, AnimatorDirection(currentAttackDirection));

        if (debugCooldown)
            Debug.Log($"[{_controller.name}] DEBUG: empieza ataque a las {Time.time:F2}; dirección = {currentAttackDirection}; cooldown configurado = {_controller.Stats.attackCooldown:F2} s", _controller);
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

    // ---------- Dirección ----------

    private Vector2 CalculateAttackDirection(Transform target)
    {
        // Se usa el centro del cuerpo (collider) y no el pivote de los pies:
        // con pivotes en los pies, una diferencia de altura entre sprites falsea el eje Y.
        Vector2 from = GetBodyCenter(_controller.transform);
        Vector2 to = GetBodyCenter(target);
        Vector2 delta = to - from;

        if (delta.sqrMagnitude <= 0.0001f)
            return currentAttackDirection != Vector2.zero ? currentAttackDirection : Vector2.right;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        // Histéresis: si el objetivo sigue "más o menos" en la dirección del ataque anterior,
        // se mantiene. Así no salta entre (1,0) y (1,1) por moverse un par de píxeles.
        if (currentAttackDirection != Vector2.zero)
        {
            float currentAngle = Mathf.Atan2(currentAttackDirection.y, currentAttackDirection.x) * Mathf.Rad2Deg;
            float halfSector = useEightDirections ? 22.5f : 45f;

            if (Mathf.Abs(Mathf.DeltaAngle(angle, currentAngle)) <= halfSector + Mathf.Max(0f, directionHysteresis))
            {
                if (debugCooldown)
                    Debug.Log($"[{_controller.name}] DEBUG: ángulo al objetivo = {angle:F1}º → se mantiene {currentAttackDirection}", _controller);
                return currentAttackDirection;
            }
        }

        Vector2 result;

        if (useEightDirections)
        {
            float snapped = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;

            // Da (1,0), (1,1), (0,1), (-1,1), (-1,0), (-1,-1), (0,-1), (1,-1)
            result = new Vector2(Mathf.Round(Mathf.Cos(snapped)), Mathf.Round(Mathf.Sin(snapped)));
        }
        else
        {
            result = Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * Mathf.Max(0.01f, verticalBias)
                ? new Vector2(0f, Mathf.Sign(delta.y))
                : new Vector2(Mathf.Sign(delta.x), 0f);
        }

        if (debugCooldown)
            Debug.Log($"[{_controller.name}] DEBUG: ángulo al objetivo = {angle:F1}º → nueva dirección {result}", _controller);

        return result;
    }

    // Lo que recibe el Animator: si la izquierda se consigue volteando el sprite, X siempre es positivo.
    private Vector2 AnimatorDirection(Vector2 direction)
    {
        if (mirrorLeftWithSpriteFlip)
            direction.x = Mathf.Abs(direction.x);
        return direction;
    }

    private void ApplyDirectionToAnimator(Vector2 direction)
    {
        if (animator == null || !useDirectionalAttackAnimation || direction == Vector2.zero) return;

        Vector2 animDir = AnimatorDirection(direction);
        animator.SetFloat(directionXParam, animDir.x);
        animator.SetFloat(directionYParam, animDir.y);
    }

    /// <summary>
    /// Distancia desde 'from' hasta el punto más cercano del cuerpo del objetivo (su collider sólido).
    /// Si no tiene collider sólido, hasta su posición.
    /// </summary>
    public static float DistanceToTarget(Vector2 from, Transform target)
    {
        if (target == null) return Mathf.Infinity;

        Collider2D body = GetBodyCollider(target);
        if (body != null)
            return Vector2.Distance(from, body.ClosestPoint(from));

        return Vector2.Distance(from, target.position);
    }

    /// <summary>Una estructura es un objetivo con vida pero sin TroopController (torres, bases...).</summary>
    public static bool IsStructure(Transform t)
    {
        return t != null && t.GetComponentInParent<TroopController>() == null;
    }

    private static Collider2D GetBodyCollider(Transform t)
    {
        Collider2D[] colliders = t.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D c in colliders)
        {
            if (c.enabled && !c.isTrigger)
                return c;
        }
        return null;
    }

    private static Vector2 GetBodyCenter(Transform t)
    {
        // Primer collider sólido (no trigger) de la tropa; los triggers suelen ser armas o rangos.
        Collider2D[] colliders = t.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D c in colliders)
        {
            if (c.enabled && !c.isTrigger)
                return c.bounds.center;
        }
        return t.position;
    }

    // ---------- Daño ----------

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
        SetAttackAnimation(false, AnimatorDirection(currentAttackDirection));
        // Se mantiene la última dirección: con (0,0) el Blend Tree no tiene clip definido
        // y durante la transición de salida puede aparecer un frame de otra dirección.
        ApplyDirectionToAnimator(currentAttackDirection);

        float cooldown = StartCooldown();

        if (debugCooldown) Debug.Log($"[{_controller.name}] DEBUG: ataque terminado; cooldown = {cooldown:F2} s.", _controller);
    }

    private float StartCooldown()
    {
        float cooldown = Mathf.Max(0f, _controller.Stats != null ? _controller.Stats.attackCooldown : 0f);
        nextAttackTime = Time.time + cooldown;
        nextCooldownDebugTime = Time.time;
        return cooldown;
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
        IsActive = false;
        _controller.HurtStarted -= HandleHurtStarted;
        _controller.HurtFinished -= HandleHurtFinished;
        _controller.AttackAnimationFinished -= FinishAttackAnimation;

        // Si la FSM cambia de estado a mitad de un ataque, el cooldown también cuenta.
        if (waitingForHit) StartCooldown();

        waitingForHit = false;
        damageAppliedThisAttack = false;
        StopMovement();
        _controller.SetMovementSpeed(0f);
        SetAttackAnimation(false, Vector2.zero);
    }

    public void OnAttack(Transform target) { }
}