using UnityEngine;

/// <summary>
/// Mirrors a camera across the water plane: reflects the Main Camera's position and rotation
/// about <see cref="waterLevel"/> and keeps the field of view in sync, so the reflected world
/// lines up with the real one (final level's "Mirror of Water").
/// </summary>
public class MirrorCameraSync : MonoBehaviour
{
    public Transform mainCamera; // Drag the Main Camera here
    public float waterLevel = 0f; // The floor's Y height

    private Camera mirrorCam;
    private Camera playerCam;

    void Start()
    {
        mirrorCam = GetComponent<Camera>();
        if (mainCamera != null)
        {
            playerCam = mainCamera.GetComponent<Camera>();
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;

        // 1. Mirror the position
        Vector3 newPos = mainCamera.position;
        newPos.y = waterLevel - (mainCamera.position.y - waterLevel);
        transform.position = newPos;

        // 2. Mirror the rotation
        Vector3 euler = mainCamera.eulerAngles;
        transform.eulerAngles = new Vector3(-euler.x, euler.y, -euler.z);

        // 3. Sync the Field of View (essential to align the scales)
        if (playerCam != null && mirrorCam != null)
        {
            mirrorCam.fieldOfView = playerCam.fieldOfView;
        }
    }
}