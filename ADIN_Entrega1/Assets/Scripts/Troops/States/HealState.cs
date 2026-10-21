
using FSM.Enemy;
using UnityEngine;

public class HealState : States<TroopController>
{
    [Header("Detección de aliados")]
    [SerializeField] private LayerMask allyLayer;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private float repathInterval = 0.3f;

    private float nextCheck;
    private float nextRepath;
    private float nextHeal;

    // Indica si se está reproduciendo la animación de curar.
    private bool isHealing;

    // Evita que el mismo Animation Event cure varias veces en una animación.
    private bool healApplied;

    // Guarda el aliado que el monje está intentando curar.
    private Transform healTarget;
    private HealthSystem targetHealth;

    public override void OnEnter()
    {
        // Reiniciar los temporizadores al entrar en el estado.
        nextCheck = 0f;
        nextRepath = 0f;
        nextHeal = 0f;
        isHealing = false;
        healApplied = false;

        healTarget = null;
        targetHealth = null;

        // Detener el movimiento y desactivar la animación de ataque o curación.
        if (_controller.Mover != null)
            _controller.Mover.Stop();

        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null)
            return;

        // Mientras se reproduce la animación, esperar al Animation Event.
        if (isHealing)
            return;

        // Si el aliado ya está curado, ha muerto o no es válido, buscar otro.
        if (!IsValidTarget())
        {
            healTarget = null;
            targetHealth = null;

            // Limitar la frecuencia de búsqueda para no comprobar aliados cada frame.
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + checkInterval;
                FindWoundedAlly();
            }

            // Si no encuentra aliados heridos, el monje permanece quieto.
            if (healTarget == null)
            {
                if (_controller.Mover != null)
                    _controller.Mover.Stop();

                _controller.SetMovementSpeed(0f);
                _controller.SetAttacking(false);
                return;
            }
        }

        float distance = Vector2.Distance(_controller.transform.position, healTarget.position);

        // detectionRange permite encontrar aliados; healingRange determina cuándo puede curarlos.
        if (distance > _controller.Stats.healingRange)
        {
            // Desactivar la animación de curar mientras se desplaza.
            _controller.SetAttacking(false);

            // Actualizar el destino periódicamente para seguir al aliado.
            if (Time.time >= nextRepath && _controller.Mover != null)
            {
                nextRepath = Time.time + repathInterval;
                _controller.Mover.MoveTo(healTarget.position);
                _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
            }

            return;
        }

        // Al llegar al alcance de curación, detenerse y mirar hacia el aliado.
        if (_controller.Mover != null)
        {
            _controller.Mover.Stop();
            _controller.Mover.FaceTowards(healTarget.position);
        }

        _controller.SetMovementSpeed(0f);

        // Iniciar la animación únicamente cuando termina el cooldown.
        if (Time.time >= nextHeal)
        {
            isHealing = true;
            healApplied = false;

            // isAttacking activa la animación de curar en el Animator.
            _controller.TriggerHealAnimation();

        }
    }

    // Lo llama TroopAnimationEvents en el fotograma exacto de la curación.
    public void ApplyHeal()
    {
        // Evitar curaciones fuera de la animación o más de una vez por ciclo.
        if (!isHealing || healApplied)
            return;

        healApplied = true;

        if (_controller.Stats == null || !IsValidTarget())
            return;

        // Aplicar la curación definida en TroopStats.
        targetHealth.Heal(_controller.Stats.healingPower);

        // El cooldown empieza en el momento real de la curación.
        nextHeal = Time.time + _controller.Stats.healingCooldown;
    }

    // Lo llama TroopAnimationEvents al final del clip de curación.
    public void FinishHeal()
    {
        if (!isHealing)
            return;

        isHealing = false;
        _controller.SetAttacking(false);
    }

    private bool IsValidTarget()
    {
        return healTarget != null && targetHealth != null && !targetHealth.IsDead && targetHealth.NeedsHealing;
    }

    private void FindWoundedAlly()
    {
        // Buscar aliados dentro del rango de detección configurado en TroopStats.
        Collider2D[] hits = Physics2D.OverlapCircleAll(_controller.transform.position, _controller.Stats.detectionRange, allyLayer);

        Transform closest = null;
        HealthSystem closestHealth = null;
        float bestDistance = float.MaxValue;

        // Obtener la salud del propio monje para evitar que se seleccione a sí mismo.
        HealthSystem ownHealth = _controller.GetComponentInChildren<HealthSystem>();

        foreach (Collider2D hit in hits)
        {
            // Buscar HealthSystem en el collider o en alguno de sus padres.
            HealthSystem health = hit.GetComponentInParent<HealthSystem>();

            if (health == null || health.IsDead)
                continue;

            // No curarse a sí mismo.
            if (health == ownHealth)
                continue;

            // Ignorar aliados que ya tienen la vida completa.
            if (!health.NeedsHealing)
                continue;

            // Elegir al aliado herido más cercano.
            float distance = ((Vector2)health.transform.position - (Vector2)_controller.transform.position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = health.transform;
                closestHealth = health;
            }
        }

        // Guardar el objetivo seleccionado y su sistema de salud.
        healTarget = closest;
        targetHealth = closestHealth;
    }

    public override void OnExit()
    {
        // Detener el movimiento y desactivar la animación al abandonar el estado.
        if (_controller.Mover != null)
            _controller.Mover.Stop();

        _controller.SetMovementSpeed(0f);

        // Limpiar el estado para no conservar referencias antiguas.
        isHealing = false;
        healApplied = false;
        healTarget = null;
        targetHealth = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (_controller == null || _controller.Stats == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_controller.transform.position, _controller.Stats.detectionRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_controller.transform.position, _controller.Stats.healingRange);
    }
}
