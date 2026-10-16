using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float maxDistance = 1000f;
    [SerializeField] private float damage = 25f;

    private Vector3 previousPosition;
    private float distanceTravelled;

    private bool initialized;
    private bool destroyed;

    private BoidBehaviour ownerBoid;

    private void Awake()
    {
        previousPosition = transform.position;
    }

    public void Initialize(Transform ownerRoot, float distanceLimit)
    {
        maxDistance = Mathf.Max(distanceLimit, 0.1f);

        previousPosition = transform.position;
        distanceTravelled = 0f;
        initialized = true;
        ownerBoid = null;

        if (ownerRoot == null)
            return;

        ownerBoid = ownerRoot.GetComponentInParent<BoidBehaviour>();

        if (ownerBoid == null)
            ownerBoid = ownerRoot.GetComponentInChildren<BoidBehaviour>();

        Collider[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider>();
        Collider[] projectileColliders = GetComponentsInChildren<Collider>();

        foreach (Collider ownerCollider in ownerColliders)
        {
            if (ownerCollider == null)
                continue;

            foreach (Collider projectileCollider in projectileColliders)
            {
                if (projectileCollider == null)
                    continue;

                Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }
    }

    private void FixedUpdate()
    {
        if (destroyed)
            return;

        if (!initialized)
        {
            previousPosition = transform.position;
            initialized = true;
        }

        Vector3 currentPosition = transform.position;
        distanceTravelled += Vector3.Distance(previousPosition, currentPosition);
        previousPosition = currentPosition;

        if (distanceTravelled >= maxDistance)
            DestroyProjectile();
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void HandleHit(Collider hitCollider)
    {
        if (destroyed || hitCollider == null)
            return;

        BoidBehaviour target = hitCollider.GetComponentInParent<BoidBehaviour>();

        if (target != null && ownerBoid == null)
            target.TakeDamage(damage);

        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        if (destroyed)
            return;

        destroyed = true;
        Destroy(gameObject);
    }
}