using UnityEngine;

// Scudo del boss del livello finale. È un IDamageFilter su Health con due
// funzioni:
//   1) BLOCCO FRONTALE quando il boss è in difesa (SetDefending), come
//      FrontalShieldBlock per il Player: i colpi frontali del Player vengono
//      annullati (è la "difesa" attiva guidata da BossDuelAI).
//   2) CONTEGGIO colpi durante la raccolta (SetShedding): ogni 'hitsPerPiece'
//      colpi del Player ANDATI A SEGNO (non bloccati) segnala al director di
//      sbloccare un pezzo.
//
// Niente più invulnerabilità totale durante la raccolta: ora il boss è
// colpibile (Health.SetMaxDamagePerHit(1) lato director → 1 HP/colpo). Resta un
// 'blockAll' usato solo come sicurezza durante il flip del cielo.
//
// NB: tieni Health.invulnerabilityDuration = 0 sul boss, così ogni swing del
// Player (un colpo per bersaglio per swing) viene contato.
[RequireComponent(typeof(Health))]
public class BossShield : MonoBehaviour, IDamageFilter
{
    [SerializeField] private MirrorDuelDirector director;

    [Tooltip("Colpi del Player a segno per staccare un pezzo (≈ lunghezza combo).")]
    [SerializeField] private int hitsPerPiece = 3;

    [Tooltip("Ampiezza totale del cono frontale entro cui la guardia blocca, in gradi.")]
    [SerializeField] private float blockAngle = 120f;

    private bool _blockAll;     // sicurezza durante il flip cielo
    private bool _defending;    // guardia alzata da BossDuelAI
    private bool _shedding;     // fase di raccolta: conta i colpi → pezzi
    private int _hitCount;

    public bool IsDefending => _defending;

    // Blocco totale temporaneo (es. durante il ribaltamento del cielo).
    public void SetInvulnerable(bool value) => _blockAll = value;

    // Guardia attiva: blocca i colpi frontali del Player. La guida BossDuelAI
    // (fasi di difesa + difesa reattiva).
    public void SetDefending(bool value) => _defending = value;

    // Conteggio colpi → pezzi attivo solo in raccolta. Resetta il contatore
    // quando si (dis)attiva, così le fasi non si "ereditano" colpi parziali.
    public void SetShedding(bool value)
    {
        _shedding = value;
        _hitCount = 0;
    }

    public bool ShouldBlock(DamageInfo info)
    {
        if (_blockAll) return true;

        bool fromPlayer = info.source != null && info.source.CompareTag("Player");

        // Difesa attiva: blocca i colpi frontali del Player (non contano, non
        // tolgono vita). Spinge il Player a colpire quando la guardia è bassa.
        if (_defending && fromPlayer && IsFrontal(info)) return true;

        // Colpo andato a segno durante la raccolta: conta verso lo sblocco pezzo.
        if (fromPlayer && _shedding)
        {
            _hitCount++;
            if (_hitCount >= hitsPerPiece)
            {
                _hitCount = 0;
                if (director != null) director.NotifyPlayerBrokePiece();
            }
        }

        return false; // Health applica il danno (cappato a 1 in raccolta, con floor di fase)
    }

    private bool IsFrontal(DamageInfo info)
    {
        Vector3 toSource = info.sourcePosition - transform.position;
        toSource.y = 0f;
        if (toSource.sqrMagnitude < 0.0001f) return true; // sopra il boss: consideralo frontale
        return Vector3.Angle(transform.forward, toSource.normalized) <= blockAngle * 0.5f;
    }
}
