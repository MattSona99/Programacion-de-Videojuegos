using UnityEngine;

public class PathTile : MonoBehaviour
{
    [Header("Colori Base")]
    public Color robotColor = new Color(0.5f, 0f, 1f); // Viola
    public Color playerColor = Color.blue;             // Blu
    public Color wrongColor = Color.red;               // Rosso
    public Color defaultColor = Color.black;           // Spenta (Nera/Invisibile)
    public Color hintColor = Color.cyan;               // Aiuto a memoria (Memory Pulse)

    [Header("Intensità Luce")]
    public float glowIntensity = 3f; // Quanto è forte la luce (aumentalo se le vedi buie)

    [Header("Collider")]
    [Tooltip("Il collider NON-trigger su cui cammina il player. Va spento sulle tile sbagliate per farlo cadere.")]
    [SerializeField] private Collider solidCollider;

    private Material myMaterial;
    private PathPuzzleManager manager;
    private Color _currentBase;
    private bool _hasColor;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null)
        {
            Debug.LogError($"[PathTile] {name}: nessun Renderer trovato — la tile non potrà accendersi.", this);
            return;
        }
        myMaterial = rend.material;
        manager = FindFirstObjectByType<PathPuzzleManager>();

        // _EMISSION va attivata via script perché Unity strippa la keyword in build
        // se il materiale non aveva emission attiva in editor.
        myMaterial.EnableKeyword("_EMISSION");

        ResetTile();
    }

    public void SetSolid(bool solid)
    {
        if (solidCollider != null) solidCollider.enabled = solid;
    }

    public void SetColor(Color baseColor)
    {
        // Idempotenza: evita set ripetuti dello stesso valore quando chiamato da OnTriggerStay.
        if (_hasColor && _currentBase == baseColor) return;
        _currentBase = baseColor;
        _hasColor = true;

        if (myMaterial == null) return;

        myMaterial.SetColor("_BaseColor", baseColor);

        float emissionWeight = (baseColor == defaultColor) ? 0f : glowIntensity;
        Color finalEmissionColor = baseColor * Mathf.LinearToGammaSpace(emissionWeight);

        myMaterial.SetColor("_EmissionColor", finalEmissionColor);
    }

    public void ResetTile()
    {
        SetColor(defaultColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Se sta cadendo (Fail) non accendiamo nulla
        if (manager != null && manager.isPlayerFalling) return;

        if (other.CompareTag("Robot"))
        {
            SetColor(robotColor);
        }
        else if (other.CompareTag("Player"))
        {
            if (manager != null) manager.CheckPlayerStep(this);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Riapplica il colore se l'Exit ha sparato per sbaglio (es. CharacterController
        // che entra-esce dal trigger a fronte degli step fisici). In modalità puzzle attivo
        // lasciamo che CheckPlayerStep abbia l'ultima parola e non facciamo nulla.
        if (manager != null && manager.isPlayerFalling) return;
        if (manager != null && manager.isPuzzleActive) return;

        if (other.CompareTag("Robot"))
        {
            SetColor(robotColor);
        }
        else if (other.CompareTag("Player"))
        {
            SetColor(playerColor);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Evitiamo spegnimenti accidentali se il player sta cadendo
        if (manager != null && manager.isPlayerFalling) return;

        if (other.CompareTag("Robot"))
        {
            // In puzzle attivo la scia di Jammo deve restare accesa finché il
            // player non riprende il controllo: lo spegnimento è centralizzato in
            // PathPuzzleManager.ClearRobotTrail(). Fuori puzzle (free exploration)
            // resta il comportamento di reset al volo.
            if (manager == null || !manager.isPuzzleActive)
            {
                ResetTile();
            }
        }
        else if (other.CompareTag("Player"))
        {
            // Spegniamo la tile se il puzzle non è iniziato o se il player è uscito
            if (manager != null && !manager.isPuzzleActive)
            {
                ResetTile();
            }
        }
    }

    public bool IsRobotLit => _hasColor && _currentBase == robotColor;

    public void ClearIfRobotLit()
    {
        if (IsRobotLit) ResetTile();
    }
}