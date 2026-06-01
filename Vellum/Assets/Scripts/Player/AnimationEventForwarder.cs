using UnityEngine;
using StarterAssets;

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