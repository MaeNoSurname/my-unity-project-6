using UnityEngine;
using UnityEngine.InputSystem;

public class CameraLead : MonoBehaviour
{
    public Transform target;

    public float lookAheadAngle = 25f;
    public float leadSmoothness = 4f;
    public float returnSmoothness = 2f;

    private Quaternion initialRelativeRotation;
    private float currentLead;

    void Start()
    {
        if (target == null)
            return;

        initialRelativeRotation = Quaternion.Inverse(target.rotation) * transform.rotation;
    }

    void LateUpdate()
    {
        if (target == null || Keyboard.current == null)
            return;

        float input = 0f;

        if (Keyboard.current.aKey.isPressed)
            input -= 1f;

        if (Keyboard.current.dKey.isPressed)
            input += 1f;

        float targetLead = input * lookAheadAngle;
        float smoothness = input != 0f ? leadSmoothness : returnSmoothness;

        currentLead = Mathf.Lerp(
            currentLead,
            targetLead,
            1f - Mathf.Exp(-smoothness * Time.deltaTime)
        );

        Quaternion baseRotation = target.rotation * initialRelativeRotation;
        Quaternion leadRotation = Quaternion.AngleAxis(currentLead, target.up) * baseRotation;

        transform.rotation = leadRotation;
    }
}