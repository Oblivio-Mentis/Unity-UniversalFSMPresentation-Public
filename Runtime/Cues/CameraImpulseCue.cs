#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class CameraImpulseCue : PresentationCue
    {
        [Min(0)] public float amplitude = 1;
        [Min(0)] public float duration = 0.1f;

        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            if (driver.cameraSink is not IUniversalFsmCameraImpulseSink sink)
                throw new InvalidOperationException("Assign a camera sink implementing IUniversalFsmCameraImpulseSink.");
            if (!float.IsFinite(amplitude) || amplitude < 0 || !float.IsFinite(duration) || duration < 0)
                throw new InvalidOperationException("Camera impulse values must be finite and nonnegative.");
            try { sink.EmitImpulse(amplitude, duration); }
            catch (Exception exception) { Debug.LogException(exception, driver.cameraSink); }
        }
    }
}
