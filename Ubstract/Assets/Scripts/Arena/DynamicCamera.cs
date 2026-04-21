using UnityEngine;

/// <summary>
/// Controls an adaptive 2D camera system that dynamically adjusts focal point and zoom level
/// based on the distance between the player and the boss, while respecting arena boundaries.
/// </summary>
[RequireComponent(typeof(Camera))]
public class DynamicCamera : MonoBehaviour
{
    [Header("Combatants")]
    public Transform player;
    public Transform enemy;

    [Header("Arena Bounds (Camera Constraints)")]
    [Tooltip("The X coordinate of the background's LEFT boundary.")]
    public float arenaMinX = -8.9f;
    [Tooltip("The X coordinate of the background's RIGHT boundary.")]
    public float arenaMaxX = 8.9f;
    [Tooltip("The Y coordinate of the background's BOTTOM boundary.")]
    public float arenaMinY = -5f;
    [Tooltip("The Y coordinate of the background's TOP boundary.")]
    public float arenaMaxY = 5f;

    [Header("Framing Settings (Y)")]
    public float yOffset = 1.5f; 

    [Header("Dynamic Zoom Settings")]
    public float sizeClose = 3.5f;
    public float sizeFar = 5f;
    public float maxDistance = 12f;

    [Header("Smoothness")]
    public float zoomSpeed = 5f;
    public float moveSpeed = 5f;

    private Camera cam;

    /// <summary>
    /// Initializes camera reference and attempts to auto-locate the Player via tag if not assigned.
    /// </summary>
    void Start()
    {
        cam = GetComponent<Camera>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    /// <summary>
    /// Executes camera logic in LateUpdate to ensure all character movement 
    /// for the current frame has already been processed by the physics engine.
    /// </summary>
    void LateUpdate()
    {
        if (player == null || enemy == null) return;

        // Perform zoom calculation first to determine the updated viewport dimensions for boundary clamping
        ZoomCamera();
        
        // Calculate the target position based on the midpoint of combatants and arena limits
        MoveCamera();
    }

    /// <summary>
    /// Linearly interpolates the camera's orthographic size based on the relative 
    /// distance between the player and the enemy.
    /// </summary>
    void ZoomCamera()
    {
        float distance = Vector2.Distance(player.position, enemy.position);
        float distancePercentage = Mathf.Clamp01(distance / maxDistance);
        float targetSize = Mathf.Lerp(sizeClose, sizeFar, distancePercentage);
        
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * zoomSpeed);
    }

    /// <summary>
    /// Calculates the midpoint between combatants and applies a clamping algorithm 
    /// that accounts for the current camera frustum size to prevent showing the world edge.
    /// </summary>
    void MoveCamera()
    {
        // Determine the focal midpoint and apply vertical offset for better framing
        Vector3 centerPoint = (player.position + enemy.position) / 2f;
        centerPoint.y += yOffset;

        // Calculate half-extents of the camera view based on orthographic size and screen aspect ratio
        float camHalfHeight = cam.orthographicSize;
        float camHalfWidth = cam.orthographicSize * cam.aspect; 

        // Clamp the focal point so the camera edges never cross the defined arena boundaries
        float clampedX = Mathf.Clamp(centerPoint.x, arenaMinX + camHalfWidth, arenaMaxX - camHalfWidth);
        float clampedY = Mathf.Clamp(centerPoint.y, arenaMinY + camHalfHeight, arenaMaxY - camHalfHeight);

        Vector3 targetPosition = new Vector3(clampedX, clampedY, transform.position.z);

        // Apply smooth dampening to the camera movement
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
    }

    /// <summary>
    /// Public interface to update the camera's enemy target, typically called during level transitions.
    /// </summary>
    /// <param name="newEnemy">The Transform of the newly spawned boss.</param>
    public void SetEnemy(Transform newEnemy)
    {
        enemy = newEnemy;
    }

    /// <summary>
    /// Visualizes the camera's movement constraints within the Unity Scene View.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = new Vector3((arenaMinX + arenaMaxX) / 2f, (arenaMinY + arenaMaxY) / 2f, 0);
        Vector3 size = new Vector3(arenaMaxX - arenaMinX, arenaMaxY - arenaMinY, 0.1f);
        Gizmos.DrawWireCube(center, size);
    }
}