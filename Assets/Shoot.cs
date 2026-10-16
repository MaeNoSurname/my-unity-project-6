using UnityEngine;
using UnityEngine.InputSystem;

public class Shoot : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Rigidbody shooterBody;

    [SerializeField] private float roundsPerMinute = 600f;
    [SerializeField] private float projectileSpeed = 800f;
    [SerializeField] private float spreadAngle = 0.3f;
    [SerializeField] private float maxProjectileDistance = 1000f;

    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private AudioSource firingAudio;

    [SerializeField] private Transform recoilingBarrel;
    [SerializeField] private float recoilDistance = 0.08f;
    [SerializeField] private float recoilSpring = 180f;
    [SerializeField] private float recoilDamping = 18f;

    private float nextShotTime;
    private float recoilOffset;
    private float recoilVelocity;

    private Vector3 barrelRestPosition;
    private bool shootingEnabled;

    private void Awake()
    {
        if (shooterBody == null)
            shooterBody = GetComponentInParent<Rigidbody>();

        if (recoilingBarrel != null)
            barrelRestPosition = recoilingBarrel.localPosition;
    }

    private void Update()
    {
        UpdateRecoil();

        if (!shootingEnabled || Mouse.current == null)
            return;

        if (!Mouse.current.leftButton.isPressed)
            return;

        if (firePoint == null || projectilePrefab == null)
            return;

        if (Time.time < nextShotTime)
            return;

        FireProjectile();

        nextShotTime = Time.time + 60f / Mathf.Max(roundsPerMinute, 1f);
    }

    public void SetShootingEnabled(bool enabled)
    {
        shootingEnabled = enabled;

        if (!enabled)
            nextShotTime = 0f;
    }

    private void FireProjectile()
    {
        Quaternion spread = Quaternion.Euler(
            Random.Range(-spreadAngle, spreadAngle),
            Random.Range(-spreadAngle, spreadAngle),
            0f
        );

        Quaternion shotRotation = firePoint.rotation * spread;

        GameObject projectile = Instantiate(
            projectilePrefab,
            firePoint.position,
            shotRotation
        );

        Projectile projectileScript = projectile.GetComponent<Projectile>();

        Transform ownerRoot = shooterBody != null
            ? shooterBody.transform.root
            : transform.root;

        if (projectileScript != null)
        {
            projectileScript.Initialize(ownerRoot, maxProjectileDistance);
        }
        else
        {
            Debug.LogWarning("Add Projectile.cs to the projectile prefab.", projectile);
        }

        if (projectile.TryGetComponent(out Rigidbody projectileBody))
        {
            projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Vector3 inheritedVelocity = shooterBody != null
                ? shooterBody.linearVelocity
                : Vector3.zero;

            projectileBody.linearVelocity =
                shotRotation * Vector3.forward * projectileSpeed
                + inheritedVelocity;
        }

        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (firingAudio != null && firingAudio.clip != null)
            firingAudio.PlayOneShot(firingAudio.clip);

        if (recoilingBarrel != null)
        {
            float springStrength = Mathf.Sqrt(Mathf.Max(recoilSpring, 1f));
            recoilVelocity += recoilDistance * springStrength;
        }
    }

    private void UpdateRecoil()
    {
        if (recoilingBarrel == null)
            return;

        float dt = Time.deltaTime;

        float springForce = -recoilOffset * recoilSpring;
        float dampingForce = -recoilVelocity * recoilDamping;

        recoilVelocity += (springForce + dampingForce) * dt;
        recoilOffset += recoilVelocity * dt;

        Vector3 firingDirection = firePoint != null
            ? firePoint.forward
            : recoilingBarrel.forward;

        Transform barrelParent = recoilingBarrel.parent;

        Vector3 localDirection = barrelParent != null
            ? barrelParent.InverseTransformDirection(firingDirection)
            : firingDirection;

        localDirection.Normalize();

        recoilingBarrel.localPosition =
            barrelRestPosition - localDirection * recoilOffset;
    }

    private void OnDisable()
    {
        recoilOffset = 0f;
        recoilVelocity = 0f;

        if (recoilingBarrel != null)
            recoilingBarrel.localPosition = barrelRestPosition;
    }
}