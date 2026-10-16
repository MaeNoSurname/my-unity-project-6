using UnityEngine;
using UnityEngine.InputSystem;

public class ShipMove : MonoBehaviour
{
    [SerializeField] private float flightSpeed = 25f;
    [SerializeField] private float acceleration = 14f;
    [SerializeField] private float deceleration = 20f;
    [SerializeField] private float slowMultiplier = 0.3f;

    [SerializeField] private float boostMultiplier = 2.2f;
    [SerializeField] private float boostCapacity = 100f;
    [SerializeField] private float boostDrain = 30f;
    [SerializeField] private float boostRecharge = 22f;

    [SerializeField] private float pitchSpeed = 100f;
    [SerializeField] private float yawSpeed = 90f;
    [SerializeField] private float rollSpeed = 180f;
    [SerializeField] private float inputSmoothTime = 0.10f;

    [SerializeField] private float straightFlightSpeed = 25f;

    private Rigidbody shipBody;

    private bool straightFlightMode;
    private bool isBoosting;

    private Vector3 straightDirection;
    private float currentSpeed;
    private float boostFuel;

    private float pitchInput;
    private float yawInput;
    private float rollInput;

    private float pitchVelocity;
    private float yawVelocity;
    private float rollVelocity;

    private float targetPitch;
    private float targetYaw;
    private float targetRoll;

    public bool IsBoosting => isBoosting;
    public float Speed => currentSpeed;
    public float BoostFuel => boostFuel;

    public float BoostPercent => boostCapacity > 0f
        ? boostFuel / boostCapacity
        : 0f;

    private void Awake()
    {
        shipBody = GetComponent<Rigidbody>();

        currentSpeed = Mathf.Max(0f, flightSpeed);
        boostFuel = Mathf.Max(0f, boostCapacity);
        straightDirection = transform.forward.normalized;

        if (shipBody != null)
        {
            shipBody.useGravity = false;
            shipBody.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void Update()
    {
        targetPitch = 0f;
        targetYaw = 0f;
        targetRoll = 0f;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || straightFlightMode)
            return;

        if (keyboard.wKey.isPressed)
            targetPitch = -1f;

        if (keyboard.sKey.isPressed)
            targetPitch = 1f;

        if (keyboard.aKey.isPressed)
            targetYaw = -1f;

        if (keyboard.dKey.isPressed)
            targetYaw = 1f;

        if (keyboard.qKey.isPressed)
            targetRoll = 1f;

        if (keyboard.eKey.isPressed)
            targetRoll = -1f;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (straightFlightMode)
        {
            isBoosting = false;
            MoveStraight(dt);
            return;
        }

        UpdateFlight(dt);
    }

    private void UpdateFlight(float dt)
    {
        pitchInput = Mathf.SmoothDamp(
            pitchInput,
            targetPitch,
            ref pitchVelocity,
            inputSmoothTime,
            Mathf.Infinity,
            dt
        );

        yawInput = Mathf.SmoothDamp(
            yawInput,
            targetYaw,
            ref yawVelocity,
            inputSmoothTime,
            Mathf.Infinity,
            dt
        );

        rollInput = Mathf.SmoothDamp(
            rollInput,
            targetRoll,
            ref rollVelocity,
            inputSmoothTime,
            Mathf.Infinity,
            dt
        );

        Keyboard keyboard = Keyboard.current;

        bool boostRequested = keyboard != null &&
            (keyboard.leftShiftKey.isPressed ||
             keyboard.rightShiftKey.isPressed);

        bool slowRequested = keyboard != null &&
            (keyboard.leftCtrlKey.isPressed ||
             keyboard.rightCtrlKey.isPressed);

        isBoosting = boostRequested && boostFuel > 0f;

        if (isBoosting)
        {
            boostFuel = Mathf.Max(0f, boostFuel - boostDrain * dt);
        }
        else
        {
            boostFuel = Mathf.MoveTowards(
                boostFuel,
                Mathf.Max(0f, boostCapacity),
                Mathf.Max(0f, boostRecharge) * dt
            );
        }

        float multiplier = 1f;

        if (isBoosting)
            multiplier = Mathf.Max(1f, boostMultiplier);
        else if (slowRequested)
            multiplier = Mathf.Clamp01(slowMultiplier);

        float targetSpeed = Mathf.Max(0f, flightSpeed * multiplier);
        float speedRate = targetSpeed > currentSpeed ? acceleration : deceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            Mathf.Max(0f, speedRate) * dt
        );

        Quaternion rotation = GetRotation();

        rotation *= Quaternion.AngleAxis(
            pitchInput * pitchSpeed * dt,
            Vector3.right
        );

        rotation *= Quaternion.AngleAxis(
            yawInput * yawSpeed * dt,
            Vector3.up
        );

        rotation *= Quaternion.AngleAxis(
            rollInput * rollSpeed * dt,
            Vector3.forward
        );

        rotation = Quaternion.Normalize(rotation);

        MoveShip(rotation, currentSpeed, dt);
    }

    private void MoveShip(Quaternion rotation, float speed, float dt)
    {
        Vector3 forward = rotation * Vector3.forward;

        if (shipBody != null)
        {
            shipBody.MoveRotation(rotation);

            if (shipBody.isKinematic)
            {
                shipBody.MovePosition(
                    shipBody.position + forward * speed * dt
                );
            }
            else
            {
                shipBody.linearVelocity = forward * speed;
            }
        }
        else
        {
            transform.rotation = rotation;
            transform.position += forward * speed * dt;
        }
    }

    private void MoveStraight(float dt)
    {
        float speed = Mathf.Max(0f, straightFlightSpeed);

        if (shipBody != null)
        {
            if (shipBody.isKinematic)
            {
                shipBody.MovePosition(
                    shipBody.position + straightDirection * speed * dt
                );
            }
            else
            {
                shipBody.linearVelocity = straightDirection * speed;
            }
        }
        else
        {
            transform.position += straightDirection * speed * dt;
        }
    }

    public void SetStraightFlightMode(bool enabled)
    {
        if (straightFlightMode == enabled)
            return;

        straightFlightMode = enabled;

        if (enabled)
        {
            straightDirection = GetRotation() * Vector3.forward;
            straightDirection.Normalize();
            isBoosting = false;
        }
        else
        {
            currentSpeed = Mathf.Max(0f, flightSpeed);

            pitchInput = 0f;
            yawInput = 0f;
            rollInput = 0f;

            pitchVelocity = 0f;
            yawVelocity = 0f;
            rollVelocity = 0f;
        }
    }

    public bool IsInStraightFlightMode()
    {
        return straightFlightMode;
    }

    private Quaternion GetRotation()
    {
        return shipBody != null ? shipBody.rotation : transform.rotation;
    }
}