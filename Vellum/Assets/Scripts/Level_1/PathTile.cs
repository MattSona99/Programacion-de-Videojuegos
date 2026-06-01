using UnityEngine;

/// <summary>
/// A single floor tile of the Act 1 path puzzle. Lights up (emissive) per role — robot trail,
/// player step, wrong, hint — and toggles its solid collider so wrong tiles drop the player.
/// Driven by trigger callbacks and by <see cref="PathPuzzleManager"/>.
/// </summary>
public class PathTile : MonoBehaviour
{
    [Header("Base colors")]
    public Color robotColor = new Color(0.5f, 0f, 1f); // Purple
    public Color playerColor = Color.blue;             // Blue
    public Color wrongColor = Color.red;               // Red
    public Color defaultColor = Color.black;           // Off (black/invisible)
    public Color hintColor = Color.cyan;               // Memory hint (Memory Pulse)

    [Header("Light intensity")]
    public float glowIntensity = 3f; // How strong the light is (raise it if the tiles look dim)

    [Header("Collider")]
    [Tooltip("The NON-trigger collider the player walks on. Disabled on wrong tiles to make the player fall.")]
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
            Debug.LogError($"[PathTile] {name}: no Renderer found — the tile won't be able to light up.", this);
            return;
        }
        myMaterial = rend.material;
        manager = FindFirstObjectByType<PathPuzzleManager>();

        // _EMISSION must be enabled via script because Unity strips the keyword in builds
        // if the material didn't have emission enabled in the editor.
        myMaterial.EnableKeyword("_EMISSION");

        ResetTile();
    }

    /// <summary>Enables/disables the walkable collider (disabled = the player falls through).</summary>
    public void SetSolid(bool solid)
    {
        if (solidCollider != null) solidCollider.enabled = solid;
    }

    /// <summary>Sets the tile's base color and emission (off when set to <see cref="defaultColor"/>).</summary>
    public void SetColor(Color baseColor)
    {
        // Idempotency: avoids repeated sets of the same value when called from OnTriggerStay.
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
        // If the player is falling (Fail), don't light anything
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
        // Reapply the color if Exit fired by mistake (e.g. a CharacterController entering/
        // exiting the trigger due to physical steps). In active puzzle mode we let
        // CheckPlayerStep have the final word and do nothing here.
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
        // Avoid accidental turn-offs if the player is falling
        if (manager != null && manager.isPlayerFalling) return;

        if (other.CompareTag("Robot"))
        {
            // In active puzzle mode Jammo's trail must stay lit until the player regains
            // control: turning it off is centralized in PathPuzzleManager.ClearRobotTrail().
            // Outside the puzzle (free exploration) the on-the-fly reset behavior remains.
            if (manager == null || !manager.isPuzzleActive)
            {
                ResetTile();
            }
        }
        else if (other.CompareTag("Player"))
        {
            // Turn the tile off if the puzzle hasn't started or the player has left
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