using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MouseRotate : MonoBehaviour
{
    public float lookSpeed = 0.2f;
    public float turnSpeed = 100f;

    public float minLookAngle = -80f;
    public float maxLookAngle = 80f;

    [SerializeField] private bool aimEnabled;
    [SerializeField] private bool aimAssistEnabled = true;
    [SerializeField] private Transform aimPoint;
    [SerializeField] private float lockRange = 250f;
    [SerializeField, Range(1f, 90f)] private float lockAngle = 8f;
    [SerializeField, Min(0.1f)] private float aimSmoothness = 5f;
    [SerializeField, Min(0.05f)] private float scanInterval = 0.15f;

    [SerializeField] private bool highlightTarget = true;
    [SerializeField] private Color highlightColor = Color.yellow;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private float xRotation;
    private float yRotation;
    private float scanTimer;

    private Quaternion initialLocalRotation;

    private BoidBehaviour lockedTarget;
    private BoidBehaviour highlightedTarget;

    private readonly List<Renderer> highlightedRenderers = new List<Renderer>();
    private readonly List<MaterialPropertyBlock> originalBlocks = new List<MaterialPropertyBlock>();

    private Transform AimReference => aimPoint != null ? aimPoint : transform;

    private void Start()
    {
        initialLocalRotation = transform.localRotation;
        xRotation = 0f;
        yRotation = 0f;
    }

    public void SetAimEnabled(bool enabled)
    {
        aimEnabled = enabled;

        if (!enabled)
            ClearTarget();
    }

    private void Update()
    {
        if (!aimEnabled)
        {
            if (lockedTarget != null || highlightedTarget != null)
                ClearTarget();

            return;
        }

        float dt = Time.deltaTime;

        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            xRotation -= mouseDelta.y * lookSpeed;
            yRotation += mouseDelta.x * lookSpeed;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
                yRotation -= turnSpeed * dt;

            if (Keyboard.current.dKey.isPressed)
                yRotation += turnSpeed * dt;
        }

        ClampAngles();
        ApplyRotation();

        if (!aimAssistEnabled)
        {
            if (lockedTarget != null || highlightedTarget != null)
                ClearTarget();

            return;
        }

        UpdateTarget(dt);

        if (lockedTarget != null)
            SmoothAimAtTarget(lockedTarget, dt);
    }

    private void ClampAngles()
    {
        xRotation = Mathf.Clamp(xRotation, minLookAngle, maxLookAngle);
        yRotation = Mathf.Repeat(yRotation + 180f, 360f) - 180f;
    }

    private void ApplyRotation()
    {
        transform.localRotation = initialLocalRotation * Quaternion.Euler(xRotation, yRotation, 0f);
    }

    private BoidBehaviour FindTarget()
    {
        Transform barrel = AimReference;
        BoidBehaviour closest = null;
        float closestDistance = lockRange;

        BoidBehaviour[] boids = FindObjectsOfType<BoidBehaviour>();

        foreach (BoidBehaviour boid in boids)
        {
            if (boid == null || !boid.gameObject.activeInHierarchy)
                continue;

            Vector3 direction = GetTargetPosition(boid) - barrel.position;
            float distance = direction.magnitude;

            if (distance <= 0.01f || distance > closestDistance)
                continue;

            if (Vector3.Angle(barrel.forward, direction) > lockAngle)
                continue;

            closest = boid;
            closestDistance = distance;
        }

        return closest;
    }

    private void UpdateTarget(float dt)
    {
        scanTimer -= dt;

        if (!IsTargetValid(lockedTarget))
        {
            lockedTarget = null;
            SetHighlightedTarget(null);
            scanTimer = 0f;
        }

        if (lockedTarget == null && scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            lockedTarget = FindTarget();
            SetHighlightedTarget(lockedTarget);
        }
    }

    private bool IsTargetValid(BoidBehaviour target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        Transform barrel = AimReference;
        Vector3 direction = GetTargetPosition(target) - barrel.position;

        if (direction.sqrMagnitude > lockRange * lockRange)
            return false;

        return Vector3.Angle(barrel.forward, direction) <= lockAngle;
    }

    private Vector3 GetTargetPosition(BoidBehaviour target)
    {
        Renderer rend = target.GetComponentInChildren<Renderer>();

        if (rend != null)
            return rend.bounds.center;

        Collider col = target.GetComponentInChildren<Collider>();

        if (col != null)
            return col.bounds.center;

        return target.transform.position;
    }

    private void SmoothAimAtTarget(BoidBehaviour target, float dt)
    {
        Transform barrel = AimReference;
        Vector3 direction = GetTargetPosition(target) - barrel.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion correction = Quaternion.FromToRotation(barrel.forward, direction.normalized);
        Quaternion desiredWorldRotation = correction * transform.rotation;

        Quaternion desiredLocalRotation = transform.parent != null
            ? Quaternion.Inverse(transform.parent.rotation) * desiredWorldRotation
            : desiredWorldRotation;

        Quaternion desiredRelative = Quaternion.Inverse(initialLocalRotation) * desiredLocalRotation;
        Vector3 angles = desiredRelative.eulerAngles;

        float targetPitch = Mathf.Clamp(
            Mathf.DeltaAngle(0f, angles.x),
            minLookAngle,
            maxLookAngle
        );

        float targetYaw = Mathf.DeltaAngle(0f, angles.y);
        float maxStep = turnSpeed * dt;
        float blend = 1f - Mathf.Exp(-aimSmoothness * dt);

        float pitchDelta = Mathf.LerpAngle(xRotation, targetPitch, blend) - xRotation;
        float yawDelta = Mathf.DeltaAngle(yRotation, targetYaw) * blend;

        Vector2 step = new Vector2(pitchDelta, yawDelta);

        if (step.magnitude > maxStep && step.magnitude > 0f)
            step = step.normalized * maxStep;

        xRotation = Mathf.Clamp(xRotation + step.x, minLookAngle, maxLookAngle);
        yRotation = Mathf.Repeat(yRotation + step.y + 180f, 360f) - 180f;

        ApplyRotation();
    }

    private void SetHighlightedTarget(BoidBehaviour target)
    {
        if (highlightedTarget == target)
            return;

        RestoreHighlight();
        highlightedTarget = target;

        if (!highlightTarget || target == null)
            return;

        foreach (Renderer rend in target.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null)
                continue;

            MaterialPropertyBlock original = new MaterialPropertyBlock();
            rend.GetPropertyBlock(original);

            originalBlocks.Add(original);
            highlightedRenderers.Add(rend);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            block.SetColor(BaseColorId, highlightColor);
            block.SetColor(ColorId, highlightColor);

            rend.SetPropertyBlock(block);
        }
    }

    private void RestoreHighlight()
    {
        for (int i = 0; i < highlightedRenderers.Count; i++)
        {
            if (highlightedRenderers[i] != null)
                highlightedRenderers[i].SetPropertyBlock(originalBlocks[i]);
        }

        highlightedRenderers.Clear();
        originalBlocks.Clear();
        highlightedTarget = null;
    }

    public void ClearTarget()
    {
        lockedTarget = null;
        scanTimer = 0f;
        RestoreHighlight();
    }

    private void OnDisable()
    {
        ClearTarget();
    }
}