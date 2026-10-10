using System;
using UnityEngine;

public class FSMController <T>: MonoBehaviour where T : FSMController<T> //Le indica a la clase que T debe heredar de FSMController<T>
{
    private States<T> currentState;

    private void Update()
    {
        if (currentState)
        {
            currentState.OnUpdate();
        }
    }

    public void SetState(States<T> newState)
    {
        if (newState == null)
        {
            Debug.LogError($"[{name}] Intento de cambiar a un estado null.");
            return;
        }

        if (currentState == newState)
        {
            Debug.Log($"[{name}] Ya está en {newState.GetType().Name}.");
            return;
        }

        Debug.Log($"[{name}] Cambio de estado: " + $"{(currentState != null ? currentState.GetType().Name : "Ninguno")} " + $"-> {newState.GetType().Name}");
        
        if (currentState)
        {
            currentState.OnExit();
            
            currentState = newState;
            currentState.OnEnter();
        }
    }
}
