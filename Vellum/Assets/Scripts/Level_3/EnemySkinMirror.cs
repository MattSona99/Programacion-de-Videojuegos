using UnityEngine;

/// <summary>
/// Final level ("The Mirror of Water"). The boss is the Player's doppelganger but of the OPPOSITE
/// SEX: when the Player swaps skin with the book (PlayerSkinSwitcher), the boss switches to the
/// other form. Reuses the same pattern as PlayerSkinSwitcher (geometry toggle + the Animator.Rebind
/// required by CLAUDE.md §3.3) but applied to the opposite.
/// </summary>
public class EnemySkinMirror : MonoBehaviour
{
    [Header("Source to mirror")]
    [SerializeField] private PlayerSkinSwitcher playerSkin;

    [Header("Boss geometries")]
    [SerializeField] private GameObject maleGeometry;
    [SerializeField] private GameObject femaleGeometry;

    void OnEnable()
    {
        if (playerSkin != null) playerSkin.SkinChanged += OnPlayerSkinChanged;
    }

    void OnDisable()
    {
        if (playerSkin != null) playerSkin.SkinChanged -= OnPlayerSkinChanged;
    }

    void Start()
    {
        // Initial state: opposite of the Player's current skin.
        if (playerSkin != null) ApplyOpposite(playerSkin.IsMale);
    }

    private void OnPlayerSkinChanged(bool playerMale) => ApplyOpposite(playerMale);

    /// <summary>Male Player → female boss, and vice versa; rebinds the active geometry's Animator.</summary>
    private void ApplyOpposite(bool playerMale)
    {
        bool bossMale = !playerMale;

        if (maleGeometry != null) maleGeometry.SetActive(bossMale);
        if (femaleGeometry != null) femaleGeometry.SetActive(!bossMale);

        // Re-align the Animator to the active geometry (without Rebind the animations
        // may stay stuck on the previous skin).
        GameObject active = bossMale ? maleGeometry : femaleGeometry;
        if (active != null && active.TryGetComponent(out Animator animator))
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }
}
