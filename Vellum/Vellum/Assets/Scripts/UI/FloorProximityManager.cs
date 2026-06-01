using UnityEngine;

public class FloorProximityManager : MonoBehaviour
{
    [Tooltip("Trascina qui il tuo Player")]
    public Transform player;
    
    private Material floorMaterial;

    void Start()
    {
        // Prende il materiale del pavimento a cui è attaccato
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // Usiamo material (e non sharedMaterial) così non modifichiamo l'asset globale
            floorMaterial = rend.material; 
        }
    }

    void Update()
    {
        if (player != null && floorMaterial != null)
        {
            // Invia costantemente le coordinate del player allo Shader!
            // IMPORTANTE: il nome tra virgolette DEVE essere identico al "Reference" del Vector3 nel tuo Shader Graph
            floorMaterial.SetVector("_PlayerPos", player.position);
        }
    }
}