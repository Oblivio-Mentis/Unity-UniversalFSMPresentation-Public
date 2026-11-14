#nullable enable
using UnityEngine;

namespace OblivioMentis.Presentation
{
    [AddComponentMenu("")]
    internal sealed class PresentationParticleReturn : MonoBehaviour
    {
        internal PresentationParticlePool Pool = null!;
        internal ParticleSystem Particles = null!;
        internal FsmPresentationDriver? Owner;
        internal bool InUse, StopOnExit;
        internal int Layer, State;

        private void OnParticleSystemStopped()
        {
            // A Stop-and-clear callback from a previous use must not release a currently playing slot.
            if (InUse && !Particles.IsAlive(false)) Pool.Release(this);
        }
    }
}
