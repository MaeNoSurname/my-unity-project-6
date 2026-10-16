using System.Collections.Generic;
using UnityEngine;

public class BoidBehaviour : MonoBehaviour
{
    public BoidManager Manager;

    public List<BoidBehaviour> NearestBoids = new();
    public List<BoidBehaviour> BoidsWithinRadius = new();

    public float Speed = 7f;
    public float TurnSpeed = 100f;
    public Vector3 FacingDirection;

    [HideInInspector] public Vector3 WanderDirection;
    [HideInInspector] public Vector3 DesiredDirection;
    [HideInInspector] public float NextWanderChange;
    [HideInInspector] public float SpeedPhase;

    [Min(1f)]
    public float MaxHealth = 100f;

    public float CurrentHealth;

    [SerializeField] private GameObject deathParticlePrefab;
    [Min(0.1f)]
    [SerializeField] private float deathParticleLifetime = 5f;

    public Rigidbody rb;

    private bool isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        FacingDirection = transform.forward;
        WanderDirection = FacingDirection;
        DesiredDirection = FacingDirection;

        SpeedPhase = Random.Range(0f, Mathf.PI * 2f);
        NextWanderChange = Time.time + Random.Range(1f, 3f);

        MaxHealth = Mathf.Max(1f, MaxHealth);
        CurrentHealth = MaxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);

        Debug.Log($"{name} took {damage} damage. Health: {CurrentHealth}/{MaxHealth}");

        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        SpawnDeathParticles();
        Destroy(gameObject);
    }

    private void SpawnDeathParticles()
    {
        if (deathParticlePrefab == null)
            return;

        GameObject effect = Instantiate(
            deathParticlePrefab,
            transform.position,
            transform.rotation
        );

        ParticleSystem[] particleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particles in particleSystems)
            particles.Play(true);

        Destroy(effect, deathParticleLifetime);
    }

    private void OnDestroy()
    {
        if (Manager != null)
            Manager.UnregisterBoid(this);
    }
}