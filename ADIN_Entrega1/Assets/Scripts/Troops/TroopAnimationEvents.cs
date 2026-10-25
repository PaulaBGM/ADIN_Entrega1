using System;
using UnityEngine;
using FSM.Enemy;

public class TroopAnimationEvents : MonoBehaviour
{
    private TroopController troopController;

    [Header("Collider del arma")]
    [SerializeField] private BoxCollider2D attackCollider;

    private bool hitDetectedThisSwing;
    private ContactFilter2D filter;
    private readonly Collider2D[] results = new Collider2D[16];

    public event Action AttackFinished;
    public event Action HitFinished;

    private void Awake()
    {
        troopController = GetComponentInParent<TroopController>();

        if (attackCollider == null) attackCollider = GetComponentInChildren<BoxCollider2D>(true);

        if (attackCollider != null) attackCollider.enabled = false;
        else Debug.LogWarning($"[{name}] No se ha encontrado el collider del arma.", this);

        if (troopController == null) Debug.LogError($"[{name}] No se ha encontrado TroopController en los padres.", this);

        filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;
    }

    // Animation Event: comienza la ventana de impacto.
    public void StartAttackHitbox()
    {
        if (attackCollider == null || troopController == null || troopController.IsHurt) return;

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
        if (attackCollider != null) attackCollider.enabled = false;
    }

    // Animation Event: termina el clip de ataque.
    public void FinishAttackAnimation()
    {
        DisableAttackHitbox();

        if (troopController == null || troopController.AttackState == null) return;

        troopController.AttackState.FinishAttackAnimation();
        AttackFinished?.Invoke();
    }

    // El collider del arma está en un hijo (HitBox) y este script en Visual, así que Unity
    // no siempre entrega OnTriggerEnter2D aquí. Por eso la ventana de impacto se comprueba
    // también con un solape directo del collider.
    private void FixedUpdate()
    {
        if (attackCollider == null || !attackCollider.enabled || hitDetectedThisSwing) return;

        int count = attackCollider.Overlap(filter, results);
        for (int i = 0; i < count; i++)
        {
            TryHit(results[i]);
            if (hitDetectedThisSwing) break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHit(Collider2D other)
    {
        if (attackCollider == null || !attackCollider.enabled || hitDetectedThisSwing) return;
        if (troopController == null || troopController.IsHurt || troopController.AttackState == null) return;

        HealthSystem health = other.GetComponentInParent<HealthSystem>();
        if (health == null || health.IsDead) return;

        if (troopController.AttackState.ApplyAttackDamageTo(health))
        {
            hitDetectedThisSwing = true;
            DisableAttackHitbox();
        }
    }

    // Animation Event: aplica la curación.
    public void ApplyHeal()
    {
        if (troopController == null || troopController.HealState == null) return;

        troopController.HealState.ApplyHeal();
    }

    // Animation Event: termina la curación.
    public void FinishHeal()
    {
        if (troopController == null || troopController.HealState == null) return;

        troopController.HealState.FinishHeal();
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
}