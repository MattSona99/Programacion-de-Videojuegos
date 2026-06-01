using UnityEngine;

/// <summary>
/// Feeds the player's position into the floor material's shader each frame (e.g. for a proximity
/// glow effect). Uses an instanced material so the shared asset isn't modified.
/// </summary>
public class FloorProximityManager : MonoBehaviour
{
    [Tooltip("Drag your Player here")]
    public Transform player;

    private Material floorMaterial;

    void Start()
    {
        // Get the material of the floor this is attached to
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // Use material (not sharedMaterial) so we don't modify the global asset
            floorMaterial = rend.material;
        }
    }

    void Update()
    {
        if (player != null && floorMaterial != null)
        {
            // Continuously send the player's coordinates to the shader.
            // IMPORTANT: the name in quotes MUST match the Vector3 "Reference" in your Shader Graph
            floorMaterial.SetVector("_PlayerPos", player.position);
        }
    }
}