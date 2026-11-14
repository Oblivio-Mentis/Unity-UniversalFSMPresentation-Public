#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class AfterimageCue : PresentationCue
    {
        public Color tint = new(0.35f, 0.8f, 1, 0.5f);
        [Min(0.001f)] public float duration = 0.2f;
        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            if (driver.afterimage == null) throw new InvalidOperationException("Assign an afterimage component.");
            driver.afterimage.Snapshot(tint, duration);
        }
    }
}
