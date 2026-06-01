using UnityEngine;

/// <summary>Spins a pickup around Y and optionally bobs it up and down with a sine curve for a floating look.</summary>
public class PickupRotator : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Rotation speed around the Y axis")]
    public float rotationSpeed = 100f;

    [Header("Bobbing (up and down)")]
    public bool enableBobbing = true;
    [Tooltip("How fast it bobs up and down")]
    public float bobbingSpeed = 2f;
    [Tooltip("How far it rises and lowers")]
    public float bobbingAmount = 0.2f;

    private Vector3 _startPosition;

    void Start()
    {
        // Save the starting position the bobbing effect originates from
        _startPosition = transform.localPosition;
    }

    void Update()
    {
        // 1. ROTATION: spins it around itself (Y axis)
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        // 2. BOBBING: uses a sine curve for smooth motion
        if (enableBobbing)
        {
            float newY = _startPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
            transform.localPosition = new Vector3(_startPosition.x, newY, _startPosition.z);
        }
    }
}