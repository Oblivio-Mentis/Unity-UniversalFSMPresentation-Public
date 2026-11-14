#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class SpriteFlashCue : PresentationCue
    {
        public Color color = Color.white;
        [Min(0.001f)] public float duration = 0.1f;
        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            if (driver.spriteFlash == null) throw new InvalidOperationException("Assign a sprite flash component.");
            driver.spriteFlash.Flash(color, duration);
        }
    }
}
