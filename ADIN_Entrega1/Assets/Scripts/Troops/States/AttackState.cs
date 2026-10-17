using FSM.Enemy;
using UnityEngine;

public class AttackState : States<TroopController>, ITroopAttack
{
    [SerializeField] private float attackRange = 0.8f;     // Distancia a la que golpea (cuerpo a cuerpo)
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float repathInterval = 0.3f;  // Cada cuánto recalcula el camino hacia el enemigo

    private float nextAttack;
    private float nextRepath;
    private HealthSystem targetHealth;

    public override void OnEnter()
    {
        targetHealth = _controller.Target != null
            ? _controller.Target.GetComponent<HealthSystem>()
            : null;

        nextRepath = 0f; // Calcular camino en el primer frame
    }

    public override void OnUpdate()
    {
        var target = _controller.Target;

        // Objetivo destruido o muerto (Destroy tiene retardo, por eso se mira IsDead)
        if (target == null || targetHealth == null || targetHealth.IsDead)
        {
            _controller.Target = null;
            _controller.SetState(_controller.WalkState);
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);

        if (dist > attackRange)
        {
            // Perseguir: recalcular el camino cada cierto tiempo, no cada frame
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                _controller.Mover.MoveTo(target.position);
            }
        }
        else
        {
            // Cuerpo a cuerpo: parar y atacar respetando el cooldown
            _controller.Mover.Stop();
            _controller.Mover.FaceTowards(target.position);

            if (Time.time >= nextAttack)
            {
                nextAttack = Time.time + attackCooldown;
                OnAttack(target);
            }
        }
    }

    public override void OnExit()
    {
        _controller.Mover.Stop();
    }

    public void OnAttack(Transform target)
    {
        // Aquí luego restarás la defensa: daño = Mathf.Max(attackDamage - defensa, 1)
        targetHealth?.TakeDamage(attackDamage);
    }
}
