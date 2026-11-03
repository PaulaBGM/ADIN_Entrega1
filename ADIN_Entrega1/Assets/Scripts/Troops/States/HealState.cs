using UnityEngine;
using FSM.Enemy;

public class HealState : States<TroopController>
{
    [Header("Detección de aliados")]
    [SerializeField] private LayerMask allyLayer;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private float repathInterval = 0.3f;

    [Header("Seguridad")]
    [Tooltip("Tiempo máximo que puede durar la curación si el Animation Event FinishHeal no llega. Ponedlo algo por encima de la duración del clip.")]
    [SerializeField] private float healMaxDuration = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private float nextCheck;
    private float nextRepath;
    private float nextHeal;     // Cooldown: NO se reinicia al aplicar la curación ni al salir del estado.
    private float healEndTime;  // Timeout de seguridad de la animación.

    private bool isHealing;
    private bool healApplied;

    private Transform healTarget;
    private HealthSystem targetHealth;
    private HealthSystem ownHealth;

    private Animator animator;
    private TroopAnimationEvents animationEvents;

    public override void OnEnter()
    {
        nextCheck = 0f;
        nextRepath = 0f;

        isHealing = false;
        healApplied = false;

        healTarget = null;
        targetHealth = null;

        ownHealth = _controller.GetComponent<HealthSystem>();
        animator = _controller.GetComponentInChildren<Animator>();
        animationEvents = _controller.GetComponentInChildren<TroopAnimationEvents>();

        if (animationEvents != null)
        {
            // Por si OnEnter se llama dos veces sin OnExit, evitamos suscripciones duplicadas.
            animationEvents.HealApplied -= HandleHealApplied;
            animationEvents.HealFinished -= HandleHealFinished;

            animationEvents.HealApplied += HandleHealApplied;
            animationEvents.HealFinished += HandleHealFinished;
        }
        else
        {
            Debug.LogError($"[{_controller.name}] No se encuentra TroopAnimationEvents.", _controller);
        }

        if (allyLayer.value == 0)
            Debug.LogWarning($"[{_controller.name}] HealState: allyLayer está vacío, nunca encontrará aliados.", _controller);

        if (animator != null)
            animator.SetBool("isHealing", false);

        StopMoving();
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null)
            return;

        // Mientras dura la animación no hacemos nada, salvo comprobar el timeout.
        if (isHealing)
        {
            if (Time.time >= healEndTime)
            {
                Debug.LogWarning($"[{_controller.name}] FinishHeal no ha llegado; se cierra la curación por timeout.", _controller);
                HandleHealFinished();
            }
            return;
        }

        if (!IsValidTarget())
        {
            healTarget = null;
            targetHealth = null;

            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + checkInterval;
                FindWoundedAlly();
            }

            if (healTarget == null)
            {
                StopMoving();
                return;
            }
        }

        float distance = Vector2.Distance(
            _controller.transform.position,
            healTarget.position
        );

        if (distance > _controller.Stats.healingRange)
        {
            _controller.SetAttacking(false);

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;

                _controller.Mover.MoveTo(healTarget.position);
                _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
            }

            return;
        }

        StopMoving();

        if (Time.time >= nextHeal)
            StartHealing();
    }

    private void StartHealing()
    {
        if (!IsValidTarget() || isHealing)
            return;

        isHealing = true;
        healApplied = false;

        // El cooldown empieza al iniciar la animación.
        nextHeal = Time.time + Mathf.Max(0f, _controller.Stats.healingCooldown);
        healEndTime = Time.time + Mathf.Max(0.1f, healMaxDuration);

        if (animator != null)
            animator.SetBool("isHealing", true);

        if (debugLogs)
            Debug.Log($"[{_controller.name}] Ha comenzado a curar a {healTarget.name}. Próxima curación en {_controller.Stats.healingCooldown}s.", _controller);
    }

    // Se ejecuta cuando TroopAnimationEvents lanza HealApplied.
    private void HandleHealApplied()
    {
        if (!isHealing || healApplied)
            return;

        healApplied = true;

        if (!IsValidTarget())
            return;

        targetHealth.Heal(_controller.Stats.healingPower);

        if (debugLogs)
            Debug.Log($"[{_controller.name}] Ha aplicado curación ({_controller.Stats.healingPower}) a {healTarget.name}.", _controller);

        // IMPORTANTE: aquí ya NO se pone nextHeal = 0f; eso anulaba el cooldown.
    }

    // Se ejecuta cuando TroopAnimationEvents lanza HealFinished (o por timeout).
    private void HandleHealFinished()
    {
        if (!isHealing)
            return;

        isHealing = false;
        healApplied = false;

        if (animator != null)
            animator.SetBool("isHealing", false);

        _controller.SetAttacking(false);

        if (debugLogs)
            Debug.Log($"[{_controller.name}] Ha terminado la animación de curación.", _controller);
    }

    private bool IsValidTarget()
    {
        return healTarget != null
            && targetHealth != null
            && !targetHealth.IsDead
            && targetHealth.NeedsHealing;
    }

    private void FindWoundedAlly()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            _controller.transform.position,
            _controller.Stats.detectionRange,
            allyLayer
        );

        float closestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            HealthSystem health = hit.GetComponentInParent<HealthSystem>();

            if (health == null || health == ownHealth)
                continue;

            if (health.IsDead || !health.NeedsHealing)
                continue;

            float distance = Vector2.Distance(
                _controller.transform.position,
                health.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                healTarget = health.transform;
                targetHealth = health;
            }
        }
    }

    private void StopMoving()
    {
        _controller.Mover.Stop();
        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);
    }

    public override void OnExit()
    {
        if (animationEvents != null)
        {
            animationEvents.HealApplied -= HandleHealApplied;
            animationEvents.HealFinished -= HandleHealFinished;
        }

        StopMoving();

        isHealing = false;
        healApplied = false;

        if (animator != null)
            animator.SetBool("isHealing", false);

        healTarget = null;
        targetHealth = null;

        // No reiniciamos nextHeal: el cooldown se mantiene aunque se salga y se vuelva a entrar al estado.
    }

    private void OnDrawGizmosSelected()
    {
        if (_controller == null || _controller.Stats == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_controller.transform.position, _controller.Stats.detectionRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_controller.transform.position, _controller.Stats.healingRange);
    }
}