using System;
using UnityEngine;
using UnityEngine.AI;

namespace FSM.Enemy
{
    public class TroopController : FSMController<TroopController>
    {
        [SerializeField] private float maxHealth;
        [SerializeField] private float timeToDestroy = 2f;
        [SerializeField] private EventManagerSO _eventManagerSo;
        
        private HealthSystem _healthSystem;
        public Transform Target { get; set; }
        public Animator Animator { get; set; }
        
        public IdleState IdleState { get; set; }
        public WalkState WalkState { get; set; }
        
        //Estados de ataque implementando ITroopAttack
        public ITroopAttack CurrentAttackState  { get; set; }
        public AttackState AttackState  { get; set; }
        public ShootAttackState ShootAttackState { get; set; }
        
        public NavMeshAgent Agent { get; set; }
        
        private float currentHealth;
        private bool isDead;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Animator = GetComponent<Animator>();
            
            IdleState = GetComponent<IdleState>();
            WalkState = GetComponent<WalkState>();
            AttackState = GetComponent<AttackState>();
            
            _healthSystem = GetComponent<HealthSystem>();

            // Inicializar los estados con el controlador
            IdleState?.InitController(this);
            WalkState?.InitController(this);
            AttackState?.InitController(this);
            
            // Inicializar CurrentAttackState (por defecto melee o ranged según componentes)
            CurrentAttackState = (ITroopAttack) ShootAttackState ?? (ITroopAttack)AttackState;
            
            currentHealth = maxHealth;
            
            //Elegir estado inicial disponible
            if (WalkState != null)
                SetState(WalkState);
            else if (IdleState != null)
                SetState(IdleState);
            else
                Debug.LogWarning($"[{name}] No tiene un estado inicial disponible.");
        }
        
        private void OnEnable()
        {
            _healthSystem.OnTroopDied += TroopDead;
        }
        
        //TEMPORAL, ES PARA HACER PRUEBAS
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I) && IdleState != null)
                SetState(IdleState);

            if (Input.GetKeyDown(KeyCode.W) && WalkState != null)
                SetState(WalkState);
        }

        private void TroopDead()
        {
            MonoBehaviour[] states = {IdleState, WalkState, AttackState};
            foreach (var state in states)
                if(state != null) state.enabled = false;
            
            StopAllCoroutines();
            
            Animator.enabled = false;
            Agent.enabled = false;
            this.enabled = false;
            
            Destroy(gameObject, timeToDestroy);
        }

        private void OnDisable()
        {
            _healthSystem.OnTroopDied -= TroopDead;
        }
    }
}