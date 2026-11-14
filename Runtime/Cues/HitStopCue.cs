#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public enum HitStopDurationSource { Authored = 0, SignalPayload = 1 }

    public sealed class HitStopCue : PresentationCue
    {
        [Range(0, 1)] public float scale;
        [Min(0.001f)] public float duration = 0.08f;
        public HitStopDurationSource durationSource;
        [Min(0)] public float payloadDurationMultiplier = 1f;
        [Min(0)] public float minimumPayloadDuration;
        [Min(0)] public float maximumPayloadDuration = 0.2f;

        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            float resolved = duration;
            if (durationSource == HitStopDurationSource.SignalPayload)
            {
                float payload = signal.DomainPayload.Duration;
                if (!Nonnegative(payload) || !Nonnegative(payloadDurationMultiplier) ||
                    !Nonnegative(minimumPayloadDuration) || !Nonnegative(maximumPayloadDuration) ||
                    maximumPayloadDuration < minimumPayloadDuration || !Nonnegative(scale) || scale > 1)
                    throw new InvalidOperationException("Hit-stop payload and tuning must be finite and nonnegative, with ordered clamps and scale [0,1].");
                // Double intermediates keep finite float inputs from overflowing before the clamp.
                resolved = (float)Math.Min(maximumPayloadDuration,
                    Math.Max(minimumPayloadDuration, (double)payload * payloadDurationMultiplier));
                if (resolved <= 0) return;
            }
            else if (durationSource != HitStopDurationSource.Authored)
                throw new InvalidOperationException("Unknown hit-stop duration source.");
            if (driver.hitStop == null) throw new InvalidOperationException("Assign a shared hit-stop coordinator.");
            driver.hitStop.RequestStop(driver, scale, resolved);
        }

        private static bool Nonnegative(float value) => float.IsFinite(value) && value >= 0;
    }
}
