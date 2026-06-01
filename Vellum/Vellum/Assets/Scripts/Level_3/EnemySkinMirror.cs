using UnityEngine;

// Livello finale ("Lo Specchio d'Acqua"). Il boss è il doppelganger del Player
// ma di SESSO OPPOSTO: quando il Player cambia skin col libro
// (PlayerSkinSwitcher), il boss passa all'altra forma. Riusa lo stesso pattern
// di PlayerSkinSwitcher (toggle delle geometrie + Animator.Rebind richiesto da
// CLAUDE.md §3.3) ma applicato all'opposto.
public class EnemySkinMirror : MonoBehaviour
{
    [Header("Sorgente da rispecchiare")]
    [SerializeField] private PlayerSkinSwitcher playerSkin;

    [Header("Geometrie del boss")]
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
        // Stato iniziale: opposto alla skin corrente del Player.
        if (playerSkin != null) ApplyOpposite(playerSkin.IsMale);
    }

    private void OnPlayerSkinChanged(bool playerMale) => ApplyOpposite(playerMale);

    // Player maschile → boss femminile, e viceversa.
    private void ApplyOpposite(bool playerMale)
    {
        bool bossMale = !playerMale;

        if (maleGeometry != null) maleGeometry.SetActive(bossMale);
        if (femaleGeometry != null) femaleGeometry.SetActive(!bossMale);

        // Riallinea l'Animator alla geometria attiva (senza Rebind le animazioni
        // possono restare bloccate sulla skin precedente).
        GameObject active = bossMale ? maleGeometry : femaleGeometry;
        if (active != null && active.TryGetComponent(out Animator animator))
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }
}
