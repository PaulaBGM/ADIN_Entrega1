using System;
using UnityEngine;
using FSM.Enemy;

public class TroopAnimationEvents : MonoBehaviour
{
    private TroopController troopController;

    [Header("Collider del arma")]
    [SerializeField] private BoxCollider2D attackCollider;

    [Header("Hitbox direccional (Lancero)")]
    [Tooltip("Si está activo, el collider del arma se coloca en la dirección del ataque.")]
    [SerializeField] private bool orientHitboxToAttackDirection = false;
    [Tooltip("Distancia desde el centro del GameObject del collider hasta el centro de la hitbox.")]
    [SerializeField] private float hitboxReach = 0.6f;

    private Vector2 baseHitboxSize;

    private bool hitDetectedThisSwing;
    private ContactFilter2D filter;
    private readonly Collider2D[] results = new Collider2D[16];

    public event Action AttackFinished;
    public event Action HitFinished;

    // Eventos de curación
    public event Action HealApplied;
    public event Action HealFinished;

    private void Awake()
    {
        troopController = GetComponentInParent<TroopController>();

        if (attackCollider == null)
            attackCollider = GetComponentInChildren<BoxCollider2D>(true);

        if (attackCollider != null)
        {
            attackCollider.enabled = false;
            // Se asume que el tamaño original es el del golpe HORIZONTAL (más ancho que alto).
            baseHitboxSize = attackCollider.size;
        }
        else
        {
            Debug.LogWarning($"[{name}] No se ha encontrado el collider del arma.", this);
        }

        if (troopController == null)
            Debug.LogError($"[{name}] No se ha encontrado TroopController en los padres.", this);

        filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;
    }

    // Llamado por AttackState al empezar cada ataque.
    public void SetAttackDirection(Vector2 direction)
    {
        if (!orientHitboxToAttackDirection || attackCollider == null || direction == Vector2.zero)
            return;

        Vector2 n = direction.normalized;

        // Si la tropa se voltea con scale.x negativo, el eje X local está invertido: se compensa.
        // Si se voltea con SpriteRenderer.flipX, lossyScale.x es positivo y no cambia nada.
        float flipSign = Mathf.Sign(attackCollider.transform.lossyScale.x);

        attackCollider.offset = new Vector2(n.x * flipSign, n.y) * hitboxReach;

        bool vertical = Mathf.Abs(n.y) > Mathf.Abs(n.x);
        attackCollider.size = vertical
            ? new Vector2(baseHitboxSize.y, baseHitboxSize.x)
            : baseHitboxSize;
    }

    // Animation Event: comienza la ventana de impacto.
    public void StartAttackHitbox()
    {
        if (attackCollider == null || troopController == null || troopController.IsHurt)
            return;

        hitDetectedThisSwing = false;
        attackCollider.enabled = true;
    }

    // Animation Event: termina la ventana de impacto.
    public void EndAttackHitbox()
    {
        DisableAttackHitbox();
    }

    public void DisableAttackHitbox()
    {
        if (attackCollider != null)
            attackCollider.enabled = false;
    }

    // Animation Event: termina el clip de ataque.
    public void FinishAttackAnimation()
    {
        DisableAttackHitbox();

        if (troopController == null || troopController.AttackState == null)
            return;

        troopController.AttackState.FinishAttackAnimation();
        AttackFinished?.Invoke();
    }

    private void FixedUpdate()
    {
        if (attackCollider == null || !attackCollider.enabled || hitDetectedThisSwing)
            return;

        int count = attackCollider.Overlap(filter, results);

        for (int i = 0; i < count; i++)
        {
            TryHit(results[i]);

            if (hitDetectedThisSwing)
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHit(Collider2D other)
    {
        if (attackCollider == null || !attackCollider.enabled || hitDetectedThisSwing)
            return;

        if (troopController == null || troopController.IsHurt || troopController.AttackState == null)
            return;

        HealthSystem health = other.GetComponentInParent<HealthSystem>();

        if (health == null || health.IsDead)
            return;

        if (troopController.AttackState.ApplyAttackDamageTo(health))
        {
            hitDetectedThisSwing = true;
            DisableAttackHitbox();
        }
    }

    // Animation Event: aplica la curación.
    public void ApplyHeal()
    {
        HealApplied?.Invoke();
    }

    // Animation Event: termina la animación de curación.
    public void FinishHeal()
    {
        HealFinished?.Invoke();
    }

    // Animation Event: termina la animación Hurt.
    public void FinishHitAnimation()
    {
        DisableAttackHitbox();

        if (troopController == null)
        {
            Debug.LogError($"[{name}] No se puede terminar Hurt: falta TroopController.", this);
            return;
        }

        Debug.Log($"[{troopController.name}] Animation Event FinishHitAnimation ejecutado.", troopController);

        troopController.FinishHitAnimation();
        HitFinished?.Invoke();
    }

    private void OnDisable()
    {
        DisableAttackHitbox();
    }

    private void OnDrawGizmosSelected()
    {
        if (attackCollider == null) return;
        Gizmos.color = attackCollider.enabled ? Color.red : new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireCube(attackCollider.bounds.center, attackCollider.bounds.size);
    }
}