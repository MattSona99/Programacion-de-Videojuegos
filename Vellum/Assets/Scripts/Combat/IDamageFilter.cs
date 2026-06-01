/// <summary>
/// Pre-damage hook: if any component on the entity returns true, the hit is cancelled.
/// Used by <see cref="FrontalShieldBlock"/> to implement frontal shield blocking.
/// </summary>
public interface IDamageFilter
{
    /// <summary>Returns true to veto (block) the incoming hit before it is applied.</summary>
    bool ShouldBlock(DamageInfo info);
}
