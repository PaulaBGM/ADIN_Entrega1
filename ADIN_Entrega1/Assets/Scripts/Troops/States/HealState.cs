using UnityEngine;
using FSM.Enemy;

/// <summary>
/// Estado del monje: cura al aliado herido más cercano. Cuando ya no quedan tropas aliadas vivas
/// pasa a modo ataque y golpea al enemigo más cercano, con el mismo rango de detección (detectionRange)
/// y de acción (healingRange) que al curar.
/// Usa los Animation Events ApplyHeal / FinishHeal de TroopAnimationEvents tanto para curar como para atacar.
/// </summary>
public class HealState : States<TroopController>
{
    private enum MonkAction { None, Heal, Attack }

    [Header("Detección")]
    [SerializeField] private LayerMask allyLayer;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private float repathInterval = 0.3f;

    [Header("Ataque del monje")]
    [SerializeField] private bool canAttack = true;
    [Tooltip("El monje solo ataca cuando ya no queda ninguna tropa viva de su equipo (las torres/bases no cuentan).")]
    [SerializeField] private bool attackOnlyWhenNoAllies = true;
    [Tooltip("Radio en el que se buscan tropas aliadas vivas. Con un valor grande cubre todo el mapa.")]
    [SerializeField] private float allyCheckRadius = 100f;
    [Tooltip("Parámetro del Animator (Bool o Trigger) para curar.")]
    [SerializeField] private string healAnimParam = "isHealing";
    [Tooltip("Parámetro del Animator (Bool o Trigger) para atacar. Si el monje no tiene animación de ataque propia, " +
             "dejad 'isHealing' y usará la de curar (con sus eventos ApplyHeal / FinishHeal).")]
    [SerializeField] private string attackAnimParam = "isHealing";

    [Header("Temporización del ataque (no depende de Animation Events)")]
    [Tooltip("Los clips de ataque del monje no traen eventos ApplyHeal/FinishHeal, así que el golpe se aplica por tiempo.")]
    [SerializeField] private string attackClipKeyword = "Attack";
    [Tooltip("Momento del clip (0-1) en que cae el golpe.")]
    [SerializeField, Range(0.05f, 1f)] private float attackImpactNormalized = 0.4f;
    [Tooltip("Duración del ataque si no se encuentra el clip.")]
    [SerializeField] private float attackFallbackDuration = 1.1f;

    [Header("Seguridad")]
    [Tooltip("Tiempo máximo de la animación si FinishHeal no llega. Ponedlo algo por encima de la duración del clip.")]
    [SerializeField] private float actionMaxDuration = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private float nextCheck;
    private float nextRepath;
    private float nextHeal;    // Cooldown de curar (no se reinicia al salir del estado)
    private float nextAttack;  // Cooldown de atacar
    private float actionEndTime;
    private float attackImpactTime;

    private MonkAction currentAction = MonkAction.None; // Acción con la animación en curso
    private string currentAnimParam;                    // Parámetro del Animator usado por esa acción
    private bool effectApplied;

    private Transform target;
    private HealthSystem targetHealth;
    private MonkAction targetType = MonkAction.None;

    private HealthSystem ownHealth;
    private Animator animator;
    private TroopAnimationEvents animationEvents;
    private TroopCombatTimer combatTimer;
    private bool attackMode;

    // ---------- Ciclo del estado ----------

    public override void OnEnter()
    {
        nextCheck = 0f;
        nextRepath = 0f;
        currentAction = MonkAction.None;
        effectApplied = false;
        ClearTarget();

        ownHealth = _controller.GetComponent<HealthSystem>();
        animator = _controller.GetComponentInChildren<Animator>();
        animationEvents = _controller.GetComponentInChildren<TroopAnimationEvents>();
        combatTimer = _controller.GetComponent<TroopCombatTimer>();

        if (animationEvents != null)
        {
            animationEvents.HealApplied -= HandleEffectApplied;
            animationEvents.HealFinished -= HandleActionFinished;
            animationEvents.HealApplied += HandleEffectApplied;
            animationEvents.HealFinished += HandleActionFinished;
        }
        else Debug.LogError($"[{_controller.name}] No se encuentra TroopAnimationEvents.", _controller);

        _controller.HurtStarted -= HandleHurtStarted;
        _controller.HurtStarted += HandleHurtStarted;

        if (allyLayer.value == 0)
            Debug.LogWarning($"[{_controller.name}] HealState: allyLayer está vacío, nunca encontrará aliados.", _controller);
        if (canAttack && enemyLayer.value == 0)
            Debug.LogWarning($"[{_controller.name}] HealState: enemyLayer está vacío, nunca encontrará enemigos.", _controller);

        SetAnim(healAnimParam, false);
        SetAnim(attackAnimParam, false);
        StopMoving();
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null || _controller.IsHurt)
            return;

        // Durante la animación solo se vigila el timeout.
        if (currentAction != MonkAction.None)
        {
            // Ataque: el golpe cae por tiempo, sin esperar a un evento que el clip no tiene.
            if (currentAction == MonkAction.Attack && !effectApplied && Time.time >= attackImpactTime)
                HandleEffectApplied();

            if (Time.time >= actionEndTime)
            {
                if (currentAction == MonkAction.Attack)
                {
                    HandleActionFinished();   // el ataque dura lo que el clip: un golpe por animación
                    return;
                }

                Debug.LogWarning($"[{_controller.name}] FinishHeal no ha llegado; se cierra la acción por timeout.", _controller);
                HandleActionFinished();
            }
            return;
        }

        // Se reevalúa cada poco, para que curar tenga prioridad sobre atacar.
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            ChooseTarget();
        }

        if (!IsValidTarget())
        {
            ClearTarget();
            StopMoving();
            return;
        }

        // Curar y atacar usan el mismo rango (healingRange) y la misma detección (detectionRange).
        float range = _controller.Stats.healingRange;
        // Hasta el borde del objetivo: así el monje también puede golpear una torre.
        float distance = AttackState.DistanceToTarget(_controller.transform.position, target);

        // Alturas: tampoco cura ni ataca a quien está en otro nivel; sube o baja hasta él.
        bool heightOk = AttackState.SameHeight(_controller.transform.position, target);

        if (distance > range || !heightOk)
        {
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                _controller.Mover.MoveTo(heightOk ? target.position : AttackState.ApproachPoint(_controller.transform.position, target, range + 2f));
                _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
            }
            return;
        }

        StopMoving();
        _controller.Mover.FaceTowards(target.position);

        if (targetType == MonkAction.Attack && combatTimer != null)
            combatTimer.MarkInRange(target);

        if (targetType == MonkAction.Heal && Time.time >= nextHeal)
            StartAction(MonkAction.Heal);
        else if (targetType == MonkAction.Attack && Time.time >= nextAttack)
            StartAction(MonkAction.Attack);
    }

    public override void OnExit()
    {
        if (animationEvents != null)
        {
            animationEvents.HealApplied -= HandleEffectApplied;
            animationEvents.HealFinished -= HandleActionFinished;
        }
        _controller.HurtStarted -= HandleHurtStarted;

        StopMoving();
        SetAnim(healAnimParam, false);
        SetAnim(attackAnimParam, false);

        currentAction = MonkAction.None;
        effectApplied = false;
        ClearTarget();
        // No se reinician nextHeal / nextAttack: los cooldowns se mantienen.
    }

    // ---------- Acciones ----------

    private void StartAction(MonkAction action)
    {
        if (!IsValidTarget() || currentAction != MonkAction.None)
            return;

        currentAction = action;
        effectApplied = false;
        actionEndTime = Time.time + Mathf.Max(0.1f, actionMaxDuration);

        if (action == MonkAction.Attack)
        {
            float length = AttackClipLength();
            attackImpactTime = Time.time + length * attackImpactNormalized;
            actionEndTime = Time.time + length;
        }

        // El cooldown empieza al iniciar la animación.
        if (action == MonkAction.Heal)
            nextHeal = Time.time + Mathf.Max(0f, _controller.Stats.healingCooldown);
        else
            nextAttack = Time.time + AttackCooldown();

        currentAnimParam = action == MonkAction.Heal ? healAnimParam : attackAnimParam;

        // Si el Animator no tiene el parámetro de ataque, se usa el de curar
        // (misma animación y mismos eventos ApplyHeal / FinishHeal).
        if (action == MonkAction.Attack && !HasAnimParam(currentAnimParam))
        {
            Debug.LogWarning($"[{_controller.name}] El Animator no tiene '{attackAnimParam}'; el ataque usa '{healAnimParam}'.", _controller);
            currentAnimParam = healAnimParam;
        }

        SetAnim(currentAnimParam, true);
    }

    // Duración del clip de ataque del Animator (se busca por nombre).
    private float AttackClipLength()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name.Contains(attackClipKeyword) && clip.length > 0.05f)
                    return clip.length;
        }

        return Mathf.Max(0.1f, attackFallbackDuration);
    }

    private bool HasAnimParam(string param)
    {
        if (animator == null || string.IsNullOrEmpty(param)) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == param) return true;
        return false;
    }

    // Animation Event ApplyHeal: aplica la curación o el daño según la acción en curso.
    private void HandleEffectApplied()
    {
        if (currentAction == MonkAction.None || effectApplied)
            return;

        effectApplied = true;

        if (!IsValidTarget())
            return;

        if (currentAction == MonkAction.Heal)
        {
            targetHealth.Heal(_controller.Stats.healingPower);
            if (debugLogs) Debug.Log($"[{_controller.name}] Cura {_controller.Stats.healingPower} a {target.name}.", _controller);
        }
        else
        {
            float damage = AttackDamage();
            targetHealth.TakeDamage(damage);
            if (debugLogs) Debug.Log($"[{_controller.name}] Golpea {damage} a {target.name}.", _controller);
        }
    }

    // Animation Event FinishHeal (o timeout): termina la animación en curso.
    private void HandleActionFinished()
    {
        if (currentAction == MonkAction.None)
            return;

        // Si el evento ApplyHeal no llegó (clip sin evento, animación que no se reprodujo...),
        // el efecto se aplica igualmente al terminar, para no perder la curación o el golpe.
        if (!effectApplied)
        {
            Debug.LogWarning($"[{_controller.name}] No llegó el evento ApplyHeal; se aplica el efecto al terminar la acción.", _controller);
            HandleEffectApplied();
        }

        SetAnim(currentAnimParam, false);

        if (debugLogs)
            Debug.Log($"[{_controller.name}] Termina de {(currentAction == MonkAction.Heal ? "curar" : "atacar")}.", _controller);

        currentAction = MonkAction.None;
        effectApplied = false;
    }

    // Si le golpean a mitad de la animación, se cancela. Si no llegó a aplicar el efecto,
    // no gasta el cooldown (igual que AttackState tras Hurt).
    private void HandleHurtStarted()
    {
        if (currentAction == MonkAction.None)
            return;

        if (!effectApplied)
        {
            if (currentAction == MonkAction.Heal) nextHeal = Time.time;
            else nextAttack = Time.time;
        }

        SetAnim(healAnimParam, false);
        SetAnim(attackAnimParam, false);
        currentAction = MonkAction.None;
        effectApplied = false;

        if (debugLogs) Debug.Log($"[{_controller.name}] Acción del monje interrumpida por daño.", _controller);
    }

    // ---------- Objetivos ----------

    private void ChooseTarget()
    {
        HealthSystem wounded = FindClosest(allyLayer, needsHealing: true);
        if (wounded != null)
        {
            SetTarget(wounded, MonkAction.Heal);
            return;
        }

        bool onlySupportLeft = TroopController.OnlyRangedAndHealersLeft();
        bool shouldAttack = canAttack && (!attackOnlyWhenNoAllies || !HasLivingAllyTroops() || onlySupportLeft);

        if (shouldAttack != attackMode)
        {
            attackMode = shouldAttack;
        }

        if (attackMode)
        {
            // Si solo quedan arqueros y monjes, el monje sale a buscar al enemigo aunque esté lejos.
            HealthSystem enemy = FindClosest(enemyLayer, needsHealing: false, onlySupportLeft ? 1000f : -1f);
            if (enemy != null)
            {
                SetTarget(enemy, MonkAction.Attack);
                return;
            }
        }

        ClearTarget();
    }

    // ¿Queda alguna TROPA viva de su equipo (sin contarse a sí mismo ni a torres/bases)?
    private bool HasLivingAllyTroops()
    {
        if (allyLayer.value == 0) return false;

        Collider2D[] hits = Physics2D.OverlapCircleAll(_controller.transform.position, allyCheckRadius, allyLayer);
        foreach (Collider2D hit in hits)
        {
            HealthSystem health = hit.GetComponentInParent<HealthSystem>();
            if (health == null || health == ownHealth || health.IsDead) continue;
            if (health.GetComponentInParent<TroopController>() == null) continue; // torre o base: no cuenta
            return true;
        }
        return false;
    }

    // Si el TroopStats del monje no tiene valores de ataque, usa los de curación.
    private float AttackCooldown()
    {
        float cd = _controller.Stats.attackCooldown;
        return Mathf.Max(0f, cd > 0f ? cd : _controller.Stats.healingCooldown);
    }

    private float AttackDamage()
    {
        float dmg = _controller.Stats.attackDamage;
        return dmg > 0f ? dmg : _controller.Stats.healingPower;
    }

    private HealthSystem FindClosest(LayerMask mask, bool needsHealing, float radius = -1f)
    {
        if (mask.value == 0) return null;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            _controller.transform.position,
            radius > 0f ? radius : _controller.Stats.detectionRange,
            mask
        );

        HealthSystem best = null;
        float bestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            HealthSystem health = hit.GetComponentInParent<HealthSystem>();

            if (health == null || health == ownHealth || health.IsDead)
                continue;
            if (needsHealing && !health.NeedsHealing)
                continue;
            if (!needsHealing && health.gameObject.layer == _controller.gameObject.layer)
                continue;   // para atacar: nunca a un aliado

            float distance = Vector2.Distance(_controller.transform.position, health.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = health;
            }
        }

        return best;
    }

    private void SetTarget(HealthSystem health, MonkAction type)
    {
        targetHealth = health;
        target = health.transform;
        targetType = type;
    }

    private void ClearTarget()
    {
        target = null;
        targetHealth = null;
        targetType = MonkAction.None;
    }

    private bool IsValidTarget()
    {
        if (target == null || targetHealth == null || targetHealth.IsDead)
            return false;

        return targetType != MonkAction.Heal || targetHealth.NeedsHealing;
    }

    // ---------- Utilidades ----------

    private void StopMoving()
    {
        _controller.Mover.Stop();
        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);
    }

    // Funciona tanto si el parámetro es Bool como si es Trigger.
    private void SetAnim(string param, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(param)) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name != param) continue;

            if (p.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(param, value);
            else if (p.type == AnimatorControllerParameterType.Trigger)
            {
                if (value) animator.SetTrigger(param);
                else animator.ResetTrigger(param);
            }
            return;
        }

        if (value)
            Debug.LogWarning($"[{_controller.name}] El Animator no tiene el parámetro '{param}'.", _controller);
    }

    private void OnDrawGizmosSelected()
    {
        if (_controller == null || _controller.Stats == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_controller.transform.position, _controller.Stats.detectionRange);

        // Curar y atacar comparten este rango.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_controller.transform.position, _controller.Stats.healingRange);
    }
}