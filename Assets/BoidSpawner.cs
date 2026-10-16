
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidSpawner : MonoBehaviour
{
    [Header("Spawning")]
    public BoidBehaviour BoidPrefab;
    public int SpawnCount = 30;
    public bool SpawnOnStart = true;

    [Min(0f)]
    public float SpawnDelay = 0.1f;

    [Header("Spawn Sphere")]
    [Min(0f)]
    public float SpawnMinRadius = 2f;

    [Min(0f)]
    public float SpawnSphereRadius = 5f;

    [Header("References")]
    public BoidManager Manager;

    [Header("Spawned Boids")]
    public List<BoidBehaviour> SpawnedBoids = new();

    private Coroutine spawnRoutine;

    void Start()
    {
        FindManager();

        if (SpawnOnStart)
            SpawnBoids();
    }

    void FindManager()
    {
        if (Manager == null)
            Manager = GetComponent<BoidManager>();

        if (Manager == null)
            Manager = GetComponentInParent<BoidManager>();
    }

    [ContextMenu("Spawn Boids")]
    public void SpawnBoids()
    {
        FindManager();

        if (BoidPrefab == null)
        {
            Debug.LogWarning(
                "BoidSpawner: Assign a Boid Prefab!", this);
            return;
        }

        if (Manager == null)
        {
            Debug.LogWarning(
                "BoidSpawner: Assign the BoidManager!", this);
            return;
        }

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnBoidsRoutine());
    }

    IEnumerator SpawnBoidsRoutine()
    {
        for (int i = 0; i < SpawnCount; i++)
        {
            // Clamp minimum radius so it never exceeds the maximum.
            float minRadius = Mathf.Clamp(
                SpawnMinRadius,
                0f,
                SpawnSphereRadius
            );

            // Random distance between the inner and outer radius.
            float radius = Random.Range(
                minRadius,
                SpawnSphereRadius
            );

            // Random direction from this GameObject's position.
            Vector3 spawnPosition = transform.position
                + Random.onUnitSphere * radius;

            // Random initial rotation.
            Vector3 direction = Random.onUnitSphere;

            Vector3 up =
                Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.98f
                ? Vector3.forward
                : Vector3.up;

            Quaternion rotation = Quaternion.LookRotation(
                direction,
                up
            );

            BoidBehaviour boid = Instantiate(
                BoidPrefab,
                spawnPosition,
                rotation
            );

            Manager.RegisterBoid(boid);
            SpawnedBoids.Add(boid);

            // Delay between spawns, except after the final boid.
            if (SpawnDelay > 0f && i < SpawnCount - 1)
                yield return new WaitForSeconds(SpawnDelay);
        }

        spawnRoutine = null;
    }

    public void ClearBoids()
    {
        // Stop any ongoing spawning.
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        // Remove all spawned boids.
        foreach (BoidBehaviour boid in SpawnedBoids)
        {
            if (boid == null)
                continue;

            if (Manager != null)
                Manager.UnregisterBoid(boid);

            Destroy(boid.gameObject);
        }

        SpawnedBoids.Clear();
    }

    void OnDrawGizmosSelected()
    {
        // Outer spawn boundary.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            transform.position,
            SpawnSphereRadius
        );

        // Inner spawn boundary.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            Mathf.Clamp(SpawnMinRadius, 0f, SpawnSphereRadius)
        );

        // Spawn center.
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}
