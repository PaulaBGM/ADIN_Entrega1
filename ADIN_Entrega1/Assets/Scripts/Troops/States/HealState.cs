
using UnityEngine;
using FSM.Enemy;

public class HealState : States<TroopController>
{
    [Header("Detección de aliados")]
    [SerializeField] private LayerMask allyLayer;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private float repathInterval = 0.3f;

    private float nextCheck;
    private float nextRepath;
    private float nextHeal;

    private bool isHealing;
    private bool healApplied;

    private Transform healTarget;
    private HealthSystem targetHealth;

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

        animator = _controller.GetComponentInChildren<Animator>();

        animationEvents = _controller.GetComponentInChildren<TroopAnimationEvents>();

        if (animationEvents != null)
        {
            animationEvents.HealApplied += HandleHealApplied;
            animationEvents.HealFinished += HandleHealFinished;
        }
        else
        {
            Debug.LogError($"[{_controller.name}] No se encuentra TroopAnimationEvents.");
        }

        if (animator != null)
            animator.SetBool("isHealing", false);

        _controller.Mover.Stop();
        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null)
            return;

        if (isHealing)
            return;

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
                _controller.Mover.Stop();
                _controller.SetMovementSpeed(0f);
                _controller.SetAttacking(false);
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

        _controller.Mover.Stop();
        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);

        if (Time.time >= nextHeal)
            StartHealing();
    }

    private void StartHealing()
    {
        if (!IsValidTarget() || isHealing)
            return;

        isHealing = true;
        healApplied = false;

        // El cooldown comienza al iniciar la animación.
        nextHeal = Time.time + Mathf.Max(0f, _controller.Stats.healingCooldown);

        if (animator != null)
            animator.SetBool("isHealing", true);

        Debug.Log($"[{_controller.name}] Ha comenzado a curar.");
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

        Debug.Log($"[{_controller.name}] Ha aplicado curación.");
    }

    // Se ejecuta cuando TroopAnimationEvents lanza HealFinished.
    private void HandleHealFinished()
    {
        if (!isHealing)
            return;

        isHealing = false;

        if (animator != null)
            animator.SetBool("isHealing", false);

        _controller.SetAttacking(false);

        Debug.Log($"[{_controller.name}] Ha terminado la animación de curación.");
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

            if (health == null)
                continue;

            if (health == _controller.GetComponent<HealthSystem>())
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

    public override void OnExit()
    {
        if (animationEvents != null)
        {
            animationEvents.HealApplied -= HandleHealApplied;
            animationEvents.HealFinished -= HandleHealFinished;
        }

        _controller.Mover.Stop();
        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);

        isHealing = false;
        healApplied = false;

        if (animator != null)
            animator.SetBool("isHealing", false);

        healTarget = null;
        targetHealth = null;

        // No reiniciamos nextHeal.
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
