using UnityEngine;
using StarterAssets;

/// <summary>
/// Sits on the mesh/Animator child and forwards StarterAssets animation events
/// (OnFootstep, OnLand) up to the <see cref="ThirdPersonController"/> on the parent,
/// since those events fire on the object that owns the Animator.
/// </summary>
public class AnimationEventForwarder : MonoBehaviour
{
    private ThirdPersonController _controller;

    void Awake()
    {
        _controller = GetComponentInParent<ThirdPersonController>();
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (_controller != null)
            _controller.SendMessage("OnFootstep", animationEvent, SendMessageOptions.DontRequireReceiver);
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (_controller != null)
            _controller.SendMessage("OnLand", animationEvent, SendMessageOptions.DontRequireReceiver);
    }
}