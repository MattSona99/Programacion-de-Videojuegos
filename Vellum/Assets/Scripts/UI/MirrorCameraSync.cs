using UnityEngine;

public class MirrorCameraSync : MonoBehaviour
{
    public Transform mainCamera; // Trascina qui la Main Camera
    public float waterLevel = 0f; // L'altezza Y del pavimento
    
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

        // 1. Specchia la posizione
        Vector3 newPos = mainCamera.position;
        newPos.y = waterLevel - (mainCamera.position.y - waterLevel);
        transform.position = newPos;

        // 2. Specchia la rotazione
        Vector3 euler = mainCamera.eulerAngles;
        transform.eulerAngles = new Vector3(-euler.x, euler.y, -euler.z);

        // 3. Sincronizza il Field of View (FONDAMENTALE per allineare le grandezze)
        if (playerCam != null && mirrorCam != null)
        {
            mirrorCam.fieldOfView = playerCam.fieldOfView;
        }
    }
}