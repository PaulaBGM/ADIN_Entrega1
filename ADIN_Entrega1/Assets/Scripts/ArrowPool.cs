
using System.Collections.Generic;
using UnityEngine;

public class ArrowPool : MonoBehaviour
{
    public static ArrowPool Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField, Min(1)] private int initialPoolSize = 30;
    [SerializeField] private bool canExpand = true;

    private readonly Queue<ArrowProjectile> availableArrows = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Ya existe un ArrowPool en la escena.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (arrowPrefab == null)
        {
            Debug.LogError("[ArrowPool] No hay prefab asignado.");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateArrow();
        }
    }

    private ArrowProjectile CreateArrow()
    {
        GameObject instance = Instantiate(arrowPrefab, transform);

        ArrowProjectile arrow = instance.GetComponent<ArrowProjectile>();

        if (arrow == null)
        {
            Debug.LogError("[ArrowPool] El prefab necesita ArrowProjectile.");
            Destroy(instance);
            return null;
        }

        instance.SetActive(false);
        return arrow;
    }

    public void Spawn(Vector2 position, Vector2 direction, float speed, float damage, GameObject owner)
    {
        ArrowProjectile arrow;

        if (availableArrows.Count > 0)
        {
            arrow = availableArrows.Dequeue();
        }
        else if (canExpand)
        {
            arrow = CreateArrow();

            if (arrow == null)
                return;
        }
        else
        {
            return;
        }

        arrow.transform.position = position;
        arrow.gameObject.SetActive(true);

        arrow.Initialize(direction, speed, damage, owner);
    }

    public void ReturnArrow(ArrowProjectile arrow)
    {
        if (arrow == null)
            return;

        arrow.ResetProjectile();

        arrow.gameObject.SetActive(false);
        arrow.transform.SetParent(transform);

        availableArrows.Enqueue(arrow);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
