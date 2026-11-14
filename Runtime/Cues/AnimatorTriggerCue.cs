#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class AnimatorTriggerCue : PresentationCue
    {
        public string parameter = string.Empty;
        public bool reset;

        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            var target = driver.animator;
            if (target == null || target.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(parameter))
                throw new InvalidOperationException("Trigger cue requires an Animator and trigger name.");
            int hash = Animator.StringToHash(parameter);
            if (!driver.HasAnimatorTrigger(target, hash)) throw new InvalidOperationException($"Animator trigger '{parameter}' does not exist.");
            if (reset) target.ResetTrigger(hash); else target.SetTrigger(hash);
        }
    }
}
