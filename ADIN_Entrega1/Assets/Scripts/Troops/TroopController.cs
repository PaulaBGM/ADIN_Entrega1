using System;
using FSM.Enemy;
using UnityEngine;

namespace FSM.Enemy
{
    public class TroopController : FSMController<TroopController>
    {
        [SerializeField] private float timeToDestroy = 2f;
        [SerializeField] private EventManagerSO _eventManagerSo;
        [SerializeField] private TroopStats stats;
        [SerializeField] private Transform defaultWaypoint;

        private HealthSystem _healthSystem;

        public Transform Waypoint { get; set; }
        public Transform Target { get; set; }

        public TroopStats Stats => stats;
        public Animator Animator { get; private set; }
        public TroopPathMover Mover { get; private set; }
        public HealState HealState { get; private set; }
        public IdleState IdleState { get; private set; }
        public WalkState WalkState { get; private set; }
        public bool IsHurt { get; private set; }
        public RangedIdleState RangedIdleState { get; private set; }

        public ITroopAttack CurrentAttackState { get; private set; }
        public AttackState AttackState { get; private set; }
        public ShootAttackState ShootAttackState { get; private set; }

        public event Action HurtStarted;
        public event Action HurtFinished;
        public event Action AttackAnimationFinished;

        private static readonly int IsHittedHash = Animator.StringToHash("isHitted");
        private static readonly int IsHealingHash = Animator.StringToHash("isHealing");
        private static readonly int IsDeadHash = Animator.StringToHash("isDead");
        private static readonly int SpeedHash = Animator.StringToHash("speed");
        private static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");
        private static readonly int IsAttacking2Hash = Animator.StringToHash("isAttacking2");
        private static readonly int DirectionXHash = Animator.StringToHash("DirectionX");
        private static readonly int DirectionYHash = Animator.StringToHash("DirectionY");

        private void Awake()
        {
            if (stats == null) Debug.LogError($"[{name}] No tiene un TroopStats asignado.", this);

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

            CurrentAttackState = ShootAttackState != null ? (ITroopAttack)ShootAttackState : AttackState;
        }

        private void Start()
        {
            if (Waypoint == null) Waypoint = defaultWaypoint;

            if (HealState != null) SetState(HealState);
            else if (ShootAttackState != null && RangedIdleState != null) SetState(RangedIdleState);
            else if (WalkState != null) SetState(WalkState);
            else if (IdleState != null) SetState(IdleState);
            else Debug.LogWarning($"[{name}] No tiene un estado inicial disponible.");
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
            MonoBehaviour[] states = { IdleState, WalkState, AttackState, ShootAttackState, RangedIdleState, HealState };
            foreach (MonoBehaviour state in states) if (state != null) state.enabled = false;

            StopAllCoroutines();

            if (Mover != null)
            {
                Mover.Stop();
                Mover.enabled = false;
            }

            ClearAttackAnimations();

            if (Animator != null && HasAnimatorParameter("isDead", AnimatorControllerParameterType.Trigger))
                Animator.SetTrigger(IsDeadHash);

            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            enabled = false;
            Destroy(gameObject, timeToDestroy);
        }

        public void ClearAttackAnimations()
        {
            if (Animator == null) return;
            if (HasAnimatorParameter("isAttacking", AnimatorControllerParameterType.Bool)) Animator.SetBool(IsAttackingHash, false);
            if (HasAnimatorParameter("isAttacking2", AnimatorControllerParameterType.Bool)) Animator.SetBool(IsAttacking2Hash, false);
        }

        public void PlayHitAnimation()
        {
            if (_healthSystem != null && _healthSystem.IsDead) return;

            IsHurt = true;
            HurtStarted?.Invoke();

            ClearAttackAnimations();
            SetMovementSpeed(0f);
            if (Mover != null) Mover.Stop();

            if (Animator != null && HasAnimatorParameter("isHitted", AnimatorControllerParameterType.Trigger))
            {
                Animator.ResetTrigger(IsHittedHash);
                Animator.SetTrigger(IsHittedHash);
            }
            else Debug.LogWarning($"[{name}] No se encontró el parámetro Trigger 'isHitted' en el Animator.", this);
        }

        public void FinishHitAnimation()
        {
            IsHurt = false;
            HurtFinished?.Invoke();

            Debug.Log($"[{name}] Hurt finalizado; IsHurt = false.", this);
        }

        public void NotifyAttackAnimationFinished()
        {
            AttackAnimationFinished?.Invoke();
        }

        public void PlayDeathAnimation()
        {
            if (Animator != null && HasAnimatorParameter("isDead", AnimatorControllerParameterType.Trigger))
                Animator.SetTrigger(IsDeadHash);
        }

        public void SetMovementSpeed(float speed)
        {
            if (Animator != null && HasAnimatorParameter("speed", AnimatorControllerParameterType.Float))
                Animator.SetFloat(SpeedHash, IsHurt ? 0f : speed);
        }

        public void SetAttacking(bool attacking)
        {
            if (Animator == null || (IsHurt && attacking)) return;

            if (HasAnimatorParameter("isAttacking", AnimatorControllerParameterType.Bool))
                Animator.SetBool(IsAttackingHash, attacking);

            if (!attacking && HasAnimatorParameter("isAttacking2", AnimatorControllerParameterType.Bool))
                Animator.SetBool(IsAttacking2Hash, false);
        }

        public void SetMeleeAttackAnimation(bool attacking, bool secondAttack, Vector2 direction)
        {
            if (Animator == null)
            {
                Debug.LogError($"[{name}] No se encontró el Animator.", this);
                return;
            }

            if (IsHurt && attacking)
            {
                ClearAttackAnimations();
                return;
            }

            if (HasAnimatorParameter("isAttacking", AnimatorControllerParameterType.Bool))
                Animator.SetBool(IsAttackingHash, attacking && !secondAttack);

            if (HasAnimatorParameter("isAttacking2", AnimatorControllerParameterType.Bool))
                Animator.SetBool(IsAttacking2Hash, attacking && secondAttack);

            if (HasAnimatorParameter("DirectionX", AnimatorControllerParameterType.Float))
                Animator.SetFloat(DirectionXHash, direction.x);

            if (HasAnimatorParameter("DirectionY", AnimatorControllerParameterType.Float))
                Animator.SetFloat(DirectionYHash, direction.y);
        }

        public void TriggerHealAnimation()
        {
            if (Animator != null && HasAnimatorParameter("isHealing", AnimatorControllerParameterType.Trigger))
                Animator.SetTrigger(IsHealingHash);
        }

        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (Animator == null || string.IsNullOrEmpty(parameterName)) return false;

            foreach (AnimatorControllerParameter parameter in Animator.parameters)
                if (parameter.name == parameterName && parameter.type == parameterType) return true;

            return false;
        }
    }
}