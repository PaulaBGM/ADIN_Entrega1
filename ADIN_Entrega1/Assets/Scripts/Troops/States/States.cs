using UnityEngine;

public abstract class States<T> : MonoBehaviour where T : FSMController<T> // T debe heredar de FSMController<T>
{
    protected T _controller;

    public virtual void InitController(T controller)
    {
        _controller = controller;
    }

    public abstract void OnEnter();  // Al entrar en el estado.
    public abstract void OnUpdate(); // Cada frame mientras se mantiene el estado.
    public abstract void OnExit();   // Al salir del estado.
}
