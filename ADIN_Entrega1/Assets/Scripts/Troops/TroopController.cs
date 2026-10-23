using UnityEngine;

namespace FSM.Enemy
{
    public class TroopController : FSMController<TroopController>
    {
        [SerializeField] private float timeToDestroy = 2f;
        [SerializeField] private EventManagerSO _eventManagerSo;
        [SerializeField] private TroopStats stats;

        [Header("Parámetros de dirección del Animator")]
        [SerializeField] private string directionXParameter = "DirectionX";
        [SerializeField] private string directionYParameter = "DirectionY";

        private HealthSystem _healthSystem;

        // Destino de la base (lo asigna el spawner) y enemigo actual
        public Transform Waypoint { get; set; }
        public Transform Target { get; set; }

        public TroopStats Stats => stats;
        public Animator Animator { get; private set; }
        public TroopPathMover Mover { get; private set; }
        public HealState HealState { get; private set; }

        public IdleState IdleState { get; private set; }
        public WalkState WalkState { get; private set; }
        public RangedIdleState RangedIdleState { get; private set; }

        // Estados de ataque (implementan ITroopAttack)
        public ITroopAttack CurrentAttackState { get; private set; }
        public AttackState AttackState { get; private set; }
        public ShootAttackState ShootAttackState { get; private set; }

        [SerializeField] private Transform defaultWaypoint; // Solo para pruebas, luego lo asigna el spawner

        private static readonly int IsHittedHash = Animator.StringToHash("isHitted");
        private static readonly int IsHealing = Animator.StringToHash("isHealing");
        private static readonly int IsDeadHash = Animator.StringToHash("isDead");
        private static readonly int SpeedHash = Animator.StringToHash("speed");
        private static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");
        private static readonly int IsAttacking2Hash = Animator.StringToHash("isAttacking2");

        private void Awake()
        {
            if (stats == null)
                Debug.LogError($"[{name}] No tiene un TroopStats asignado.", this);

            Mover = GetComponent<TroopPathMover>();
            Animator = GetComponentInChildren<Animator>();
            _healthSystem = GetComponent<HealthSystem>();

            IdleState = GetComponent<IdleState>();
            WalkState = GetComponent<WalkState>();
            AttackState = GetComponent<AttackState>();
            ShootAttackState = GetComponent<ShootAttackState>();
            RangedIdleState = GetComponent<RangedIdleState>();
            HealState = GetComponent<HealState>();

            IdleState?.InitController(this);
            WalkState?.InitController(this);
            AttackState?.InitController(this);
            ShootAttackState?.InitController(this);
            RangedIdleState?.InitController(this);
            HealState?.InitController(this);

            // Por defecto ranged si existe el componente, si no melee
            CurrentAttackState = ShootAttackState != null ? (ITroopAttack)ShootAttackState : AttackState;
        }

        private void Start()
        {
            if (Waypoint == null)
                Waypoint = defaultWaypoint;

            if (HealState != null)
                SetState(HealState);
            else if (ShootAttackState != null && RangedIdleState != null)
                SetState(RangedIdleState);
            else if (WalkState != null)
                SetState(WalkState);
            else if (IdleState != null)
                SetState(IdleState);
            else
                Debug.LogWarning($"[{name}] No tiene un estado inicial disponible.");
        }

        private void OnEnable()
        {
            if (_healthSystem != null) _healthSystem.OnTroopDied += TroopDead;
        }

        private void OnDisable()
        {
            if (_healthSystem != null) _healthSystem.OnTroopDied -= TroopDead;
        }

        private void TroopDead()
        {
            // Detiene los estados para evitar que sigan actuando tras morir
            MonoBehaviour[] states = { IdleState, WalkState, AttackState, ShootAttackState, RangedIdleState };

            foreach (var state in states)
                if (state != null) state.enabled = false;

            StopAllCoroutines();

            if (Mover != null)
            {
                Mover.Stop();
                Mover.enabled = false;
            }

            // No desactivar el Animator para permitir la animación de muerte.
            if (Animator != null)
            {
                if (HasAnimatorParameter("isAttacking", AnimatorControllerParameterType.Bool))
                    Animator.SetBool(IsAttackingHash, false);

                if (HasAnimatorParameter("isAttacking2", AnimatorControllerParameterType.Bool))
                    Animator.SetBool(IsAttacking2Hash, false);
            }

            var col = GetComponent<Collider2D>();
            if (col != null)
                col.enabled = false;

            enabled = false;

            Destroy(gameObject, timeToDestroy);
        }

        public void PlayHitAnimation()
        {
            if (Animator != null)
                Animator.SetTrigger(IsHittedHash);
        }

        public void PlayDeathAnimation()
        {
            if (Animator != null)
                Animator.SetTrigger(IsDeadHash);
        }

        public void SetMovementSpeed(float speed)
        {
            if (Animator != null)
                Animator.SetFloat(SpeedHash, speed);
        }

        public void SetAttacking(bool attacking)
        {
            if (Animator != null)
                Animator.SetBool(IsAttackingHash, attacking);
        }
        
        // Lanza la animación de curación una sola vez.
        public void TriggerHealAnimation()
        {
            if (HasAnimatorParameter("isHealing", AnimatorControllerParameterType.Trigger))
                Animator.SetTrigger(IsHealing);
        }

        // Activa la animación de ataque y actualiza la dirección del lancero.
        public void SetMeleeAttackAnimation(bool attacking, bool secondAttack, Vector2 direction)
        {
            // Activar la animación de ataque correspondiente solo si el parámetro existe.
            if (HasAnimatorParameter("isAttacking", AnimatorControllerParameterType.Bool))
                Animator.SetBool(IsAttackingHash, attacking && !secondAttack);

            if (HasAnimatorParameter("isAttacking2", AnimatorControllerParameterType.Bool))
                Animator.SetBool(IsAttacking2Hash, attacking && secondAttack);

            // Actualizar la dirección solo si este Animator tiene esos parámetros.
            if (HasAnimatorParameter(directionXParameter, AnimatorControllerParameterType.Float))
                Animator.SetFloat(directionXParameter, direction.x);

            if (HasAnimatorParameter(directionYParameter, AnimatorControllerParameterType.Float))
                Animator.SetFloat(directionYParameter, direction.y);
        }

        // Comprueba que el parámetro existe y que es del tipo esperado.
        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (Animator == null || string.IsNullOrEmpty(parameterName))
                return false;

            foreach (AnimatorControllerParameter parameter in Animator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == parameterType)
                    return true;
            }

            return false;
        }
    }
}
