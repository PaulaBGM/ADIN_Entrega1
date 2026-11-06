using FSM.Enemy;
using UnityEngine;

public class ShootAttackState : States<TroopController>, ITroopAttack
{
    [Header("Disparo")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float arrowSpeed = 10f;

    [Header("Torres")]
    [SerializeField] private float structureRangeBonus = 0.25f;
    [SerializeField] private float repathInterval = 0.4f;

    private float nextAttackTime;
    private float nextRepath;
    private int lastShootClipIndex = -1;
    
    public override void OnEnter()
    {
        nextAttackTime = 0f;

        if (_controller.Mover != null)
            _controller.Mover.Stop();

        _controller.SetMovementSpeed(0f);
        _controller.SetAttacking(false);
    }

    public override void OnUpdate()
    {
        if (_controller.Stats == null)
            return;

        Transform target = _controller.Target;

        if (target == null)
        {
            ReturnToIdle();
            return;
        }

        HealthSystem health = target.GetComponentInParent<HealthSystem>();

        if (health == null || health.IsDead)
        {
            ReturnToIdle();
            return;
        }

        bool structure = AttackState.IsStructure(target);
        float distance = structure
            ? AttackState.DistanceToTarget(_controller.transform.position, target)
            : Vector2.Distance(_controller.transform.position, target.position);
        float range = _controller.Stats.attackRange + (structure ? structureRangeBonus : 0f);

        if (distance > range)
        {
            _controller.SetAttacking(false);

            // Torre, o solo quedan arqueros y monjes: el arquero se acerca hasta tener el objetivo a tiro.
            if ((structure || TroopController.OnlyRangedAndHealersLeft()) && _controller.Mover != null && Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                _controller.Mover.MoveTo(structure ? AttackState.GetBodyCenter(target) : (Vector2)target.position);
                _controller.SetMovementSpeed(_controller.Stats.movementSpeed);
            }
            return;
        }

        if (_controller.Mover != null && _controller.Mover.IsMoving)
        {
            _controller.Mover.Stop();
            _controller.SetMovementSpeed(0f);
        }

        if (_controller.Mover != null) _controller.Mover.FaceTowards(target.position);

        _controller.SetAttacking(true);

        if (Time.time >= nextAttackTime)
        {
            Debug.Log($"Disparo permitido. Distancia: {distance}, Rango: {_controller.Stats.attackRange}");

            OnAttack(target);
            nextAttackTime = Time.time + _controller.Stats.attackCooldown;
        }
    }

    public void OnAttack(Transform target)
    {
        if (target == null || arrowPrefab == null || firePoint == null)
        {
            Debug.LogWarning($"[{_controller.name}] Falta el objetivo, Arrow Prefab o Fire Point.");
            return;
        }

        if (ArrowPool.Instance == null)
        {
            Debug.LogWarning("[FLECHA] No existe ArrowPool en la escena.");
            return;
        }

        Vector2 origin = firePoint.position;
        // Torres: se apunta al centro de su collider (el pivote queda por encima y la flecha pasaría de largo).
        Vector2 destination = AttackState.IsStructure(target) ? AttackState.GetBodyCenter(target) : (Vector2)target.position;
        Vector2 direction = (destination - origin).normalized;

        if (direction.sqrMagnitude == 0f)
            return;
        
        PlayShootSound();

        ArrowPool.Instance.Spawn(origin, direction, arrowSpeed, _controller.Stats.attackDamage, _controller.gameObject, target);
    }

    private void ReturnToIdle()
    {
        _controller.SetAttacking(false);
        _controller.Target = null;

        if (_controller.RangedIdleState != null)
            _controller.SetState(_controller.RangedIdleState);
        else if (_controller.IdleState != null)
            _controller.SetState(_controller.IdleState);
    }

    private void PlayShootSound()
    {
        if (_controller.AudioSource == null || _controller.Stats == null)
            return;

        AudioClip[] clips = _controller.Stats.attackSfx;

        if (clips == null || clips.Length == 0)
            return;

        // Crear una lista de índices de clips que no sean null.
        System.Collections.Generic.List<int> validIndexes = new();

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                validIndexes.Add(i);
        }

        if (validIndexes.Count == 0)
            return;

        // Evitar repetir el último sonido si hay más de uno disponible.
        if (validIndexes.Count > 1)
            validIndexes.Remove(lastShootClipIndex);

        int randomIndex = validIndexes[
            UnityEngine.Random.Range(0, validIndexes.Count)
        ];

        _controller.AudioSource.PlayOneShot(clips[randomIndex]);

        lastShootClipIndex = randomIndex;
    }
    
    public override void OnExit()
    {
        _controller.SetAttacking(false);
    }
}
