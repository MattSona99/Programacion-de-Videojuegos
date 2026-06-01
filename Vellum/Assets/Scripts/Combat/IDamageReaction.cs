/// <summary>
/// Post-damage hook: invoked after damage has been applied.
/// Used by <see cref="KnockbackReceiver"/> to push the hit enemy back, and by
/// <see cref="EnemyAI"/> to react to being hit by the Player.
/// </summary>
public interface IDamageReaction
{
    /// <summary>Called once damage has been applied to this entity.</summary>
    void OnDamaged(DamageInfo info);
}
