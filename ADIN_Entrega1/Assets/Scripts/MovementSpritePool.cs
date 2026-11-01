using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementSpritePool : MonoBehaviour
{
    public static MovementSpritePool Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private GameObject spritePrefab;
    [SerializeField, Min(1)] private int initialPoolSize = 20;
    [SerializeField] private bool canExpand = true;

    [Header("Duración")]
    [SerializeField, Min(0.1f)] private float spriteLifetime = 1f;

    private readonly Queue<GameObject> availableSprites = new Queue<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Ya existe un MovementSpritePool en la escena.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateSprite();
        }
    }

    private GameObject CreateSprite()
    {
        GameObject sprite = Instantiate(spritePrefab, transform);
        sprite.SetActive(false);
        availableSprites.Enqueue(sprite);
        return sprite;
    }

    public void Spawn(Vector3 position)
    {
        if (spritePrefab == null) return;

        GameObject sprite;

        if (availableSprites.Count > 0)
        {
            sprite = availableSprites.Dequeue();
        }
        else if (canExpand)
        {
            sprite = Instantiate(spritePrefab, transform);
        }
        else
        {
            return;
        }

        sprite.transform.position = position;
        sprite.transform.rotation = Quaternion.identity;
        sprite.SetActive(true);

        StartCoroutine(ReturnToPool(sprite));
    }

    private IEnumerator ReturnToPool(GameObject sprite)
    {
        yield return new WaitForSeconds(spriteLifetime);

        if (sprite == null) yield break;

        sprite.SetActive(false);
        availableSprites.Enqueue(sprite);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}