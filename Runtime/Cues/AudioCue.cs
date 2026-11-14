#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class AudioCue : PresentationCue
    {
        public AudioClip? clip;
        [Range(0, 1)] public float volume = 1;

        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            if (driver.audioSource == null || clip == null || !float.IsFinite(volume) || volume < 0 || volume > 1)
                throw new InvalidOperationException("Audio cue requires a source, clip and finite volume in [0,1].");
            driver.audioSource.PlayOneShot(clip, volume);
        }
    }
}
