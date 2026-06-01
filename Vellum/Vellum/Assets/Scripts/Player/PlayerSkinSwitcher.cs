using System;
using UnityEngine;
using StarterAssets;

public class PlayerSkinSwitcher : MonoBehaviour
{
    [Header("Look del Player")]
    public GameObject maleGeometry;
    public GameObject femaleGeometry;

    // Vero = forma maschile attiva. Lo specchio del boss (EnemySkinMirror) legge
    // questo e si pone sempre all'opposto.
    public bool IsMale { get; private set; } = true;

    // Emesso a ogni cambio skin col valore corrente (true = maschile). Usato da
    // EnemySkinMirror per rispecchiare il Player al sesso opposto.
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

    public void SwitchToMale() => Switch(true);
    public void SwitchToFemale() => Switch(false);

    private void Switch(bool male)
    {
        IsMale = male;

        // 1. Attiva/disattiva le geometrie
        if (maleGeometry != null) maleGeometry.SetActive(male);
        if (femaleGeometry != null) femaleGeometry.SetActive(!male);

        // 2. Notifica gli script che l'Animator attivo è cambiato
        if (_thirdPersonController != null) _thirdPersonController.RebindAnimator();
        if (_playerCombat != null) _playerCombat.RefreshAnimator();

        // 3. Notifica chi rispecchia la skin (boss del livello finale)
        SkinChanged?.Invoke(male);
    }
}