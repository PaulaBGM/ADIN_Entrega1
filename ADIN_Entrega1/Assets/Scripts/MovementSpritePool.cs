
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementSpritePool : MonoBehaviour
{
    public static MovementSpritePool Instance { get; private set; }

    [System.Serializable]
    public class PoolEntry
    {
        public GameObject prefab;
        [Min(1)] public int initialSize = 10;
        [Min(0.1f)] public float lifetime = 1f;
    }

    [Header("Prefabs y configuración")]
    [SerializeField] private GameObject spritePrefab;
    [SerializeField, Min(1)] private int initialPoolSize = 20;
    [SerializeField] private bool canExpand = true;
    [SerializeField, Min(0.1f)] private float spriteLifetime = 1f;

    [Header("VFX adicionales")]
    [SerializeField] private List<PoolEntry> additionalPools = new();

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
    private readonly Dictionary<GameObject, GameObject> prefabByInstance = new();
    private readonly Dictionary<GameObject, float> lifetimeByPrefab = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Ya existe un MovementSpritePool en la escena.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        RegisterPool(spritePrefab, initialPoolSize, spriteLifetime);

        foreach (PoolEntry entry in additionalPools)
        {
            if (entry == null || entry.prefab == null)
                continue;

            RegisterPool(entry.prefab, entry.initialSize, entry.lifetime);
        }
    }

    private void RegisterPool(GameObject prefab, int size, float lifetime)
    {
        if (prefab == null || pools.ContainsKey(prefab))
            return;

        pools[prefab] = new Queue<GameObject>();
        lifetimeByPrefab[prefab] = lifetime;

        for (int i = 0; i < size; i++)
            CreateInstance(prefab);
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        instance.SetActive(false);

        prefabByInstance[instance] = prefab;
        pools[prefab].Enqueue(instance);

        return instance;
    }

    // Mantiene la compatibilidad con los VFX de los pasos.
    public void Spawn(Vector3 position)
    {
        Spawn(spritePrefab, position, Quaternion.identity);
    }

    // Permite solicitar cualquier prefab registrado.
    public void Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null || !pools.ContainsKey(prefab))
            return;

        Queue<GameObject> pool = pools[prefab];

        GameObject instance;

        if (pool.Count > 0)
        {
            instance = pool.Dequeue();
        }
        else if (canExpand)
        {
            instance = CreateInstance(prefab);
            pool.Dequeue();
        }
        else
        {
            return;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        StartCoroutine(ReturnToPool(instance, lifetimeByPrefab[prefab]));
    }

    private IEnumerator ReturnToPool(GameObject instance, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        if (instance == null)
            yield break;

        instance.SetActive(false);

        if (prefabByInstance.TryGetValue(instance, out GameObject prefab) && pools.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool.Enqueue(instance);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}