#nullable enable
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    /// <summary>Standalone, reusable cue. Keep per-owner mutable state on scene components.</summary>
    public abstract class PresentationCue : ScriptableObject
    {
        public abstract void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal);
        /// <summary>Release consumer effects on reset, rebind or disable. Must be idempotent.</summary>
        public virtual void Stop(FsmPresentationDriver driver) { }
    }
}
