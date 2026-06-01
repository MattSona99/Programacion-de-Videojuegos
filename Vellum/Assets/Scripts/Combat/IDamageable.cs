/// <summary>
/// Implemented by anything that can receive damage (Player, enemies, Jammo, boss).
/// The attacker calls <see cref="TakeDamage"/> with a <see cref="DamageInfo"/> payload.
/// </summary>
public interface IDamageable
{
    /// <summary>Applies a single instance of damage to this entity.</summary>
    void TakeDamage(DamageInfo info);
}
