using UnityEngine;

public class FSMController<T> : MonoBehaviour where T : FSMController<T> // T debe heredar de FSMController<T>
{
    private States<T> currentState;

    public States<T> CurrentState => currentState;

    protected virtual void Update()
    {
        if (currentState != null)
            currentState.OnUpdate();
    }

    public void SetState(States<T> newState)
    {
        if (newState == null)
        {
            Debug.LogError($"[{name}] Intento de cambiar a un estado null.");
            return;
        }

        if (currentState == newState)
            return;

        Debug.Log($"[{name}] Cambio de estado: {(currentState != null ? currentState.GetType().Name : "Ninguno")} -> {newState.GetType().Name}");

        if (currentState != null)
            currentState.OnExit();

        currentState = newState;
        currentState.OnEnter();
    }
}
