#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class AnimatorStateCue : PresentationCue
    {
        [Tooltip("Full Animator state path, e.g. Base Layer.Run")]
        public string state = "Base Layer.Idle";
        [Min(0)] public int layer;
        [Min(0)] public float fadeSeconds = 0.1f;

        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            var target = driver.animator;
            if (target == null || target.runtimeAnimatorController == null)
                throw new InvalidOperationException("Animator cue requires a configured Animator.");
            if (layer < 0 || layer >= target.layerCount || !float.IsFinite(fadeSeconds) || fadeSeconds < 0)
                throw new InvalidOperationException("Animator layer or fadeSeconds is invalid.");
            int hash = Animator.StringToHash(state);
            if (!target.HasState(layer, hash)) throw new InvalidOperationException($"Animator state '{state}' does not exist on layer {layer}.");
            target.CrossFadeInFixedTime(hash, fadeSeconds, layer);
        }
    }
}
