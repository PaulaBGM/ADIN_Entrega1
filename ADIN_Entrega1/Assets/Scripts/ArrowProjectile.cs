
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ArrowProjectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 5f;

    private Rigidbody2D rb;
    private float damage;
    private bool hasHit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }

    public void Initialize(
        Vector2 direction,
        float speed,
        float damageAmount,
        GameObject owner)
    {
        damage = damageAmount;

        // Ignorar todos los colliders de la tropa que dispara.
        if (owner != null)
        {
            Collider2D[] ownerColliders =
                owner.GetComponentsInChildren<Collider2D>();

            Collider2D[] arrowColliders =
                GetComponentsInChildren<Collider2D>();

            foreach (Collider2D ownerCollider in ownerColliders)
            {
                foreach (Collider2D arrowCollider in arrowColliders)
                {
                    if (ownerCollider != null && arrowCollider != null)
                    {
                        Physics2D.IgnoreCollision(
                            arrowCollider,
                            ownerCollider
                        );
                    }
                }
            }
        }

        rb.linearVelocity = direction.normalized * speed;

        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit)
            return;

        HealthSystem health =
            other.GetComponentInParent<HealthSystem>();

        if (health == null || health.IsDead)
            return;

        hasHit = true;

        Debug.Log(
            $"Flecha impactó a {health.name}. Daño: {damage}"
        );

        health.TakeDamage(damage);

        Destroy(gameObject);
    }
}
