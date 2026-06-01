// Hook pre-danno: se un componente sull'entità ritorna true, il colpo è annullato.
// Usato da FrontalShieldBlock per il blocco frontale dello scudo.
public interface IDamageFilter
{
    bool ShouldBlock(DamageInfo info);
}
