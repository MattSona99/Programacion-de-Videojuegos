using UnityEngine;

/// <summary>
/// Goes on the Attack04 state (full-body finisher) of the Base Layer. The finisher is a
/// whole-body spin: it notifies <see cref="PlayerCombat"/> on enter/exit so it zeroes the
/// UpperBody layer weight (the masked legs/root there would break the rotation) and locks
/// movement for its duration. On the Boss (a clone without PlayerCombat) the calls are
/// no-ops and the state is never reached (the Boss never sets "Finisher").
/// </summary>
public class MeleeFinisherState : StateMachineBehaviour
{
    private PlayerCombat _combat;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_combat == null) _combat = animator.GetComponentInParent<PlayerCombat>();
        if (_combat != null) _combat.OnFinisherStart();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_combat != null) _combat.OnFinisherEnd();
    }
}
