using UnityEngine;

// Va sullo stato Attack04 (finisher full-body) del Base Layer. Il finisher è una
// piroetta a corpo intero: avvisa PlayerCombat in entrata/uscita così che azzeri il
// peso del layer UpperBody (le gambe/root mascherate lì spezzerebbero la rotazione)
// e blocchi il movimento finché dura. Sul Boss (clone senza PlayerCombat) i metodi
// sono no-op e lo stato non viene mai raggiunto (il Boss non setta "Finisher").
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
