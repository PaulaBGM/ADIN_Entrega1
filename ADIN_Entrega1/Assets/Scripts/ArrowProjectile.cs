using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ArrowProjectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 5f;

    private Rigidbody2D rb;
    private float damage;
    private float elapsedTime;
    private bool hasHit;
    private bool isReturning;
    private HealthSystem targetHealth;

    private Collider2D[] arrowColliders;
    private readonly List<Collider2D> ignoredOwnerColliders = new();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        arrowColliders = GetComponentsInChildren<Collider2D>(true);
    }

    public void Initialize(Vector2 direction, float speed, float damageAmount, GameObject owner, Transform target)
    {
        damage = damageAmount;
        hasHit = false;
        isReturning = false;
        elapsedTime = 0f;
        targetHealth = target != null ? target.GetComponentInParent<HealthSystem>() : null;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        foreach (Collider2D oldCollider in ignoredOwnerColliders)
        {
            if (oldCollider == null)
                continue;

            foreach (Collider2D arrowCollider in arrowColliders)
            {
                if (arrowCollider != null)
                    Physics2D.IgnoreCollision(arrowCollider, oldCollider, false);
            }
        }

        ignoredOwnerColliders.Clear();

        if (owner != null)
        {
            Collider2D[] ownerColliders = owner.GetComponentsInChildren<Collider2D>(true);

            foreach (Collider2D ownerCollider in ownerColliders)
            {
                if (ownerCollider == null)
                    continue;

                foreach (Collider2D arrowCollider in arrowColliders)
                {
                    if (arrowCollider != null)
                        Physics2D.IgnoreCollision(arrowCollider, ownerCollider, true);
                }

                ignoredOwnerColliders.Add(ownerCollider);
            }
        }

        rb.linearVelocity = direction.normalized * speed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (isReturning)
            return;

        elapsedTime += Time.deltaTime;

        if (elapsedTime >= lifeTime)
            ReturnToPool();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || isReturning)
            return;

        HealthSystem health = other.GetComponentInParent<HealthSystem>();

        if (health == null)
            return;

        if (targetHealth == null || health != targetHealth)
            return;

        if (health.IsDead)
        {
            ReturnToPool();
            return;
        }

        hasHit = true;

        Debug.Log($"[DAÑO APLICADO] Flecha ID: {GetInstanceID()} | Objetivo: {health.name} | Daño: {damage}");

        health.TakeDamage(damage);

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (isReturning)
            return;

        isReturning = true;

        if (ArrowPool.Instance != null)
            ArrowPool.Instance.ReturnArrow(this);
        else
            gameObject.SetActive(false);
    }

    public void ResetProjectile()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        damage = 0f;
        elapsedTime = 0f;
        hasHit = false;
        isReturning = false;
        targetHealth = null;
    }
}
