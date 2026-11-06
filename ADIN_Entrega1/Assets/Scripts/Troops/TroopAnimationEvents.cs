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
    private bool swingOpen;
    private float hitboxLocalX;
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
            hitboxLocalX = Mathf.Abs(attackCollider.transform.localPosition.x);
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
        swingOpen = true;
        ApplyHitboxFacing();
        attackCollider.enabled = true;

        // Torres: el golpe cuenta si están a rango, aunque la hitbox no llegue a tocar su collider.
        if (troopController.AttackState != null && troopController.AttackState.TryApplyStructureHit())
        {
            hitDetectedThisSwing = true;
            DisableAttackHitbox();
        }
    }

    // Animation Event: termina la ventana de impacto.
    public void EndAttackHitbox()
    {
        ResolveSwing();
        DisableAttackHitbox();
    }

    // Si la hitbox no tocó a nadie pero el objetivo está a rango, el golpe cuenta igualmente.
    private void ResolveSwing()
    {
        if (!swingOpen) return;
        swingOpen = false;

        if (hitDetectedThisSwing || troopController == null || troopController.IsHurt || troopController.AttackState == null) return;

        if (troopController.AttackState.TryApplyInRangeHit())
            hitDetectedThisSwing = true;
    }

    public void DisableAttackHitbox()
    {
        swingOpen = false;

        if (attackCollider != null)
            attackCollider.enabled = false;
    }

    // Animation Event: termina el clip de ataque.
    public void FinishAttackAnimation()
    {
        ResolveSwing();
        DisableAttackHitbox();

        if (troopController == null || troopController.AttackState == null)
            return;

        troopController.AttackState.FinishAttackAnimation();
        AttackFinished?.Invoke();
    }

    // La tropa se gira solo con SpriteRenderer.flipX, que no mueve el hijo HitBox: sin esto, el arma del
    // guerrero que mira a la izquierda (el morado) golpearía siempre en el lado derecho. Se coloca delante,
    // mirando hacia donde mira la tropa. El lancero no entra: sus clips ya colocan la hitbox por dirección.
    private void ApplyHitboxFacing()
    {
        if (attackCollider == null || troopController == null) return;

        AttackState attack = troopController.AttackState;
        if (attack == null || attack.UsesDirectionalAttack || troopController.Mover == null) return;

        float sign = troopController.Mover.FacingLeft ? -1f : 1f;

        Transform t = attackCollider.transform;
        Vector3 lp = t.localPosition;
        lp.x = hitboxLocalX * sign;
        t.localPosition = lp;

        Vector2 off = attackCollider.offset;
        off.x = 0f;   // el offset X de los clips está pensado para un solo lado
        attackCollider.offset = off;
    }

    private void LateUpdate()
    {
        if (swingOpen) ApplyHitboxFacing();   // después del Animator, que anima el offset del collider
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