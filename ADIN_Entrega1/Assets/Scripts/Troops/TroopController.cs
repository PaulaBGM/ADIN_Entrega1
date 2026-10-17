using UnityEngine;

namespace FSM.Enemy
{
    public class TroopController : FSMController<TroopController>
    {
        [SerializeField] private float timeToDestroy = 2f;
        [SerializeField] private EventManagerSO _eventManagerSo;

        private HealthSystem _healthSystem;

        // Destino de la base (lo asigna el spawner) y enemigo actual
        public Transform Waypoint { get; set; }
        public Transform Target { get; set; }

        public Animator Animator { get; private set; }
        public TroopPathMover Mover { get; private set; }

        public IdleState IdleState { get; private set; }
        public WalkState WalkState { get; private set; }

        // Estados de ataque (implementan ITroopAttack)
        public ITroopAttack CurrentAttackState { get; private set; }
        public AttackState AttackState { get; private set; }
        public ShootAttackState ShootAttackState { get; private set; }
        
        [SerializeField] private Transform defaultWaypoint; // solo para pruebas, luego lo asigna el spawner

        private void Awake()
        {
            Mover = GetComponent<TroopPathMover>();
            Animator = GetComponent<Animator>();
            _healthSystem = GetComponent<HealthSystem>();

            IdleState = GetComponent<IdleState>();
            WalkState = GetComponent<WalkState>();
            AttackState = GetComponent<AttackState>();
            ShootAttackState = GetComponent<ShootAttackState>();

            IdleState?.InitController(this);
            WalkState?.InitController(this);
            AttackState?.InitController(this);
            ShootAttackState?.InitController(this);

            // Por defecto ranged si existe el componente, si no melee
            CurrentAttackState = ShootAttackState != null
                ? (ITroopAttack)ShootAttackState
                : AttackState;
        }

        private void Start()
        {
            if (Waypoint == null) Waypoint = defaultWaypoint;
            // En Start (no en Awake) para que el spawner haya podido asignar el Waypoint
            if (WalkState != null)
                SetState(WalkState);
            else if (IdleState != null)
                SetState(IdleState);
            else
                Debug.LogWarning($"[{name}] No tiene un estado inicial disponible.");
        }

        private void OnEnable()
        {
            if (_healthSystem != null)
                _healthSystem.OnTroopDied += TroopDead;
        }

        private void OnDisable()
        {
            if (_healthSystem != null)
                _healthSystem.OnTroopDied -= TroopDead;
        }

        private void TroopDead()
        {
            MonoBehaviour[] states = { IdleState, WalkState, AttackState, ShootAttackState };
            foreach (var state in states)
                if (state != null) state.enabled = false;

            StopAllCoroutines();

            if (Mover != null)
            {
                Mover.Stop();
                Mover.enabled = false;
            }

            if (Animator != null) Animator.enabled = false;

            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            enabled = false;

            Destroy(gameObject, timeToDestroy);
        }
    }
}
