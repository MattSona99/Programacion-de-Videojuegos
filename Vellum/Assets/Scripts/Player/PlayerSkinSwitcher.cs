using System;
using UnityEngine;
using StarterAssets;

/// <summary>
/// Hot-swaps the Player's 3D model (male/female geometry) at runtime. After switching it
/// rebinds the active Animator (ThirdPersonController + PlayerCombat) and raises
/// <see cref="SkinChanged"/> so the final-boss mirror (EnemySkinMirror) can mirror it.
/// </summary>
public class PlayerSkinSwitcher : MonoBehaviour
{
    [Header("Player look")]
    public GameObject maleGeometry;
    public GameObject femaleGeometry;

    /// <summary>True = male form active. The boss mirror (EnemySkinMirror) reads this and always takes the opposite.</summary>
    public bool IsMale { get; private set; } = true;

    /// <summary>
    /// Raised on every skin change with the current value (true = male). Used by
    /// EnemySkinMirror to mirror the Player to the opposite sex.
    /// </summary>
    public event Action<bool> SkinChanged;

    private PlayerCombat _playerCombat;
    private ThirdPersonController _thirdPersonController;

    void Awake()
    {
        _playerCombat = GetComponent<PlayerCombat>();
        _thirdPersonController = GetComponent<ThirdPersonController>();
    }

    void Start()
    {
        SwitchToMale();
    }

    /// <summary>Switches to the male model.</summary>
    public void SwitchToMale() => Switch(true);

    /// <summary>Switches to the female model.</summary>
    public void SwitchToFemale() => Switch(false);

    private void Switch(bool male)
    {
        IsMale = male;

        // 1. Enable/disable the geometries
        if (maleGeometry != null) maleGeometry.SetActive(male);
        if (femaleGeometry != null) femaleGeometry.SetActive(!male);

        // 2. Notify scripts that the active Animator has changed
        if (_thirdPersonController != null) _thirdPersonController.RebindAnimator();
        if (_playerCombat != null) _playerCombat.RefreshAnimator();

        // 3. Notify whoever mirrors the skin (final-level boss)
        SkinChanged?.Invoke(male);
    }
}