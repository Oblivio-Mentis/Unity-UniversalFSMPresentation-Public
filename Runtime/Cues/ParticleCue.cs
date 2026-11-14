#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class ParticleCue : PresentationCue
    {
        public ParticleSystem? prefab;
        [Tooltip("Empty uses the driver transform.")]
        public string anchor = string.Empty;
        public bool stopOnStateExit;

        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            Transform target = driver.transform;
            if (!string.IsNullOrEmpty(anchor))
                target = driver.anchors?.Resolve(anchor).target ?? throw new InvalidOperationException($"Missing anchor '{anchor}'.");
            foreach (var pool in driver.particlePools)
                if (pool != null && pool.Prefab == prefab && prefab != null)
                {
                    pool.TryPlay(driver, target.position, target.rotation, signal.LayerId, signal.StateId, stopOnStateExit);
                    return;
                }
            throw new InvalidOperationException("Particle cue requires a matching prewarmed pool.");
        }
    }
}
