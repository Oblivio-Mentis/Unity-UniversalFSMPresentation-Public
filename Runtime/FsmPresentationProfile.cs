#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    [CreateAssetMenu(menuName = "FSM/Presentation/Profile")]
    public sealed class FsmPresentationProfile : ScriptableObject
    {
        public UniversalFsmDefinitionAsset? definition;
        public PresentationBinding[] bindings = Array.Empty<PresentationBinding>();
    }

    public enum PresentationEvent { StateEnter, StateExit, Transition, DomainEvent }

    [Serializable]
    public sealed class PresentationBinding
    {
        public PresentationEvent trigger;
        public int layerId;
        [Min(0)] public int stateId;
        [Min(0)] public int toStateId;
        public string eventKey = string.Empty;
        public PresentationCue[] actions = Array.Empty<PresentationCue>();
    }
}
