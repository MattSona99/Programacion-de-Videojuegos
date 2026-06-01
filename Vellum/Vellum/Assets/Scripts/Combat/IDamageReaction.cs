// Hook post-danno: invocato dopo che il danno è stato applicato.
// Usato da KnockbackReceiver per spingere all'indietro il nemico colpito.
public interface IDamageReaction
{
    void OnDamaged(DamageInfo info);
}
