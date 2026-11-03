using UnityEngine;
using FSM.Enemy;

/// <summary>
/// Debug de tiempos de combate de una tropa:
///  - Cuánto tarda en entrar en rango de su objetivo (desde que empieza el combate).
///  - Cuánto tarda en morir desde que entra en combate (primer momento en rango o primer golpe recibido).
/// TroopController lo añade solo a cada tropa. Todos los mensajes empiezan por [TIEMPOS] para filtrarlos en la consola.
/// </summary>
public class TroopCombatTimer : MonoBehaviour
{
    // ---------- Inicio global del combate ----------

    /// <summary>Momento en que empezó el combate (Time.time). -1 si nadie ha llamado a StartBattle().</summary>
    public static float BattleStartTime { get; private set; } = -1f;

    /// <summary>Llamar al empezar el combate (por ejemplo, al pulsar Enter en TroopPlacementManager).</summary>
    public static void StartBattle()
    {
        BattleStartTime = Time.time;
        Debug.Log($"[TIEMPOS] Empieza el combate (t = {Time.time:F2} s).");
    }

    // Por si "Enter Play Mode" no recarga el dominio: que no quede el valor de la partida anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => BattleStartTime = -1f;

    // ---------- Por tropa ----------

    [SerializeField] private bool logTimes = true;

    private TroopController controller;
    private HealthSystem health;

    private float firstMoveTime = -1f;   // Referencia de reserva si no se llamó a StartBattle()
    private float inRangeTime = -1f;
    private float engagedTime = -1f;     // Entra en combate: primera vez en rango o primer golpe recibido

    public float TimeToReachRange => inRangeTime >= 0f && ReferenceStart >= 0f ? inRangeTime - ReferenceStart : -1f;

    private float ReferenceStart => BattleStartTime >= 0f ? BattleStartTime : firstMoveTime;
    private string ReferenceName => BattleStartTime >= 0f ? "desde el inicio del combate" : "desde que empezó a moverse";

    private void Awake()
    {
        controller = GetComponent<TroopController>();
        health = GetComponent<HealthSystem>();
    }

    private void OnEnable()
    {
        if (health != null) health.OnTroopDied += HandleDied;
        if (controller != null) controller.HurtStarted += HandleHurt;
    }

    private void OnDisable()
    {
        if (health != null) health.OnTroopDied -= HandleDied;
        if (controller != null) controller.HurtStarted -= HandleHurt;
    }

    private void Update()
    {
        if (controller == null) return;

        if (firstMoveTime < 0f && controller.Mover != null && controller.Mover.IsMoving)
            firstMoveTime = Time.time;

        // Melee y arqueros: objetivo del controller dentro de attackRange.
        // (El monje lo avisa él mismo desde HealState, porque usa healingRange.)
        if (inRangeTime < 0f && controller.Stats != null && controller.Target != null)
        {
            float distance = AttackState.DistanceToTarget(transform.position, controller.Target);
            if (distance <= controller.Stats.attackRange)
                MarkInRange(controller.Target);
        }
    }

    /// <summary>Marca la primera vez que la tropa tiene a su objetivo a rango. Las siguientes llamadas se ignoran.</summary>
    public void MarkInRange(Transform target)
    {
        if (inRangeTime >= 0f) return;

        inRangeTime = Time.time;
        MarkEngaged();

        if (!logTimes) return;

        string who = target != null ? target.name : "su objetivo";
        string time = ReferenceStart >= 0f ? $"{inRangeTime - ReferenceStart:F2} s {ReferenceName}" : "(sin referencia de inicio)";
        Debug.Log($"[TIEMPOS] {name} entra en rango de {who}: {time}.", this);
    }

    private void HandleHurt()
    {
        MarkEngaged();
    }

    private void MarkEngaged()
    {
        if (engagedTime < 0f) engagedTime = Time.time;
    }

    private void HandleDied()
    {
        if (!logTimes) return;

        float now = Time.time;
        string sinceEngaged = engagedTime >= 0f
            ? $"{now - engagedTime:F2} s desde que entró en combate"
            : "sin haber llegado a entrar en combate";
        string sinceStart = ReferenceStart >= 0f
            ? $" ({now - ReferenceStart:F2} s {ReferenceName})"
            : "";

        Debug.Log($"[TIEMPOS] {name} muere a los {sinceEngaged}{sinceStart}.", this);
    }
}