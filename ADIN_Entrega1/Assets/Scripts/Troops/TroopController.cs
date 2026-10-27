using System;
using FSM.Enemy;
using UnityEngine;

namespace FSM.Enemy
{
    public class TroopController : FSMController<TroopController>
    {
        [SerializeField] private float timeToDestroy = 2f;

        [Header("Hurt")]
        [SerializeField, Tooltip("Parte del nombre del clip de hurt (p. ej. 'Hitted'). Sirve para detectar cuándo acaba sin depender de un evento de animación.")]
        private string hurtClipKeyword = "Hitted";
        [SerializeField, Tooltip("Tiempo máximo de hurt por si algo falla.")]
        private float hurtMaxTime = 1f;

        private Coroutine hurtRoutine;
        private int attack1State;
        private int attack2State;
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
            CacheAttackStates();
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

            // Detecta el fin del hurt sin depender de un evento de animación
            if (hurtRoutine != null) StopCoroutine(hurtRoutine);
            hurtRoutine = StartCoroutine(WaitHurtEnd());
        }

        private System.Collections.IEnumerator WaitHurtEnd()
        {
            float start = Time.time;
            bool entered = false;

            while (Time.time - start < hurtMaxTime)
            {
                yield return null;

                if (Animator == null) break;

                AnimatorClipInfo[] clips = Animator.GetCurrentAnimatorClipInfo(0);
                bool inHurt = clips.Length > 0 && clips[0].clip != null &&
                              clips[0].clip.name.Contains(hurtClipKeyword);

                if (inHurt) entered = true;
                else if (entered) break; // Ya salió del clip de hurt

                if (inHurt && Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f) break;
            }

            hurtRoutine = null;
            FinishHitAnimation();
        }

        public void FinishHitAnimation()
        {
            if (!IsHurt) return; // Ya terminado (por evento o por la rutina)

            if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }

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

            // El giro a la izquierda ya lo hace TroopPathMover (escala X de Visual, hitbox incluido),
            // así que el Blend Tree solo necesita el clip de la derecha.
            if (Mover != null && Mover.MirrorsWithScale) direction.x = Mathf.Abs(direction.x);

            if (HasAnimatorParameter("DirectionX", AnimatorControllerParameterType.Float))
                Animator.SetFloat(DirectionXHash, direction.x);

            if (HasAnimatorParameter("DirectionY", AnimatorControllerParameterType.Float))
                Animator.SetFloat(DirectionYHash, direction.y);

            if (attacking) ForceAttackState(secondAttack);
        }

        // Los clips de ataque se localizan por el nombre ("Attack1" / "Attack2").
        private void CacheAttackStates()
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return;

            foreach (AnimationClip clip in Animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.Contains("Attack2")) attack2State = Animator.StringToHash(clip.name);
                else if (clip.name.Contains("Attack1")) attack1State = Animator.StringToHash(clip.name);
            }

            // Lancero: el ataque es un Blend Tree llamado "AttackBT" (4 direcciones)
            int blendTree = Animator.StringToHash("AttackBT");
            if (attack1State == 0 && Animator.HasState(0, blendTree)) attack1State = blendTree;
        }

        // Entra directamente en el estado de ataque aunque el Animator esté en Guard
        // (no hay transición Guard -> Attack).
        private void ForceAttackState(bool secondAttack)
        {
            int hash = secondAttack ? attack2State : attack1State;
            if (hash == 0 || !Animator.HasState(0, hash)) return;
            if (Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash) return;

            Animator.CrossFadeInFixedTime(hash, 0f);
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