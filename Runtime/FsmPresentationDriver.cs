#nullable enable
using System;
using System.Collections.Generic;
using OblivioMentis.UniversalFSM;
using Unity.Profiling;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    /// <summary>Main-thread coordinator. Configure before enabling; rebind after configuration changes.</summary>
    [DisallowMultipleComponent, AddComponentMenu("FSM/Presentation/Driver")]
    public sealed class FsmPresentationDriver : MonoBehaviour
    {
        public UniversalFsmRuntimeController? controller;
        public FsmPresentationProfile? profile;
        public Animator? animator;
        [Tooltip("Dedicated source: reset/disable stops all its one-shots.")]
        public AudioSource? audioSource;
        public PresentationAnchorRegistry? anchors;
        public PresentationParticlePool[] particlePools = Array.Empty<PresentationParticlePool>();
        public PresentationAfterimage? afterimage;
        public PresentationSpriteFlash? spriteFlash;
        public PresentationHitStop? hitStop;
        public MonoBehaviour? cameraSink;

        private IUniversalFsmStateChangeSource? _source;
        private IUniversalFsmSignalBus? _bus;
        private readonly Dictionary<(PresentationEvent, int, int, int, string), List<(PresentationCue cue, bool builtIn)>> _bindings = new();
        private readonly Dictionary<int, int> _canonicalIds = new();
        private readonly HashSet<PresentationCue> _executed = new();
        private readonly List<PresentationCue> _cleanup = new();
        private readonly HashSet<int> _triggerHashes = new();
        private RuntimeAnimatorController? _triggerController;
        private bool _started;
        private bool _resetting;
        private int _revision;
        private static readonly ProfilerMarker DispatchMarker = new("Presentation.Dispatch");

        private void Start()
        {
            _started = true;
            if (_source == null && controller != null) BindController();
        }

        private void OnEnable()
        {
            if (_started && controller != null) BindController();
        }

        private void OnDisable() => Unbind();

        private void BindController()
        {
            if (controller!.Runtime is not IUniversalFsmStateChangeSource source)
                throw new InvalidOperationException("Presentation requires an initialized controller state-change source.");
            if (profile != null && profile.definition != controller.RuntimeDefinitionAsset)
                throw new InvalidOperationException("Presentation profile must reference the controller's runtime definition.");
            Bind(source);
        }

        /// <summary>Observe an existing runtime and optional domain bus. Does not initialize or tick the FSM.</summary>
        public void Bind(IUniversalFsmStateChangeSource source, IUniversalFsmSignalBus? domainBus = null)
        {
            if (!isActiveAndEnabled || _resetting)
                throw new InvalidOperationException("Enable the driver before binding; do not bind during cleanup.");
            if (source == null) throw new ArgumentNullException(nameof(source));
            Unbind();
            BuildBindings();
            _source = source;
            _bus = domainBus;
            source.LayerStateChanged += OnStateChanged;
            if (_bus != null) _bus.SignalPublished += OnDomainSignal;
            if (source is IUniversalFsmCurrentLayerStateSource current)
            {
                // Setup only. This capability returns copied count, not required capacity.
                var states = new UniversalFsmCurrentLayerState[4];
                int count;
                while ((count = current.CopyCurrentLayerStates(states)) == states.Length)
                    states = new UniversalFsmCurrentLayerState[checked(states.Length * 2)];
                int revision = _revision;
                for (int i = 0; i < count && revision == _revision; i++)
                    Dispatch(PresentationEvent.StateEnter, UniversalFsmSignal.LayerEntered(
                        states[i].LayerId, states[i].LayerKey, Canonical(states[i].CurrentStateId), string.Empty));
            }
        }

        public void Unbind()
        {
            if (_source != null) _source.LayerStateChanged -= OnStateChanged;
            if (_bus != null) _bus.SignalPublished -= OnDomainSignal;
            _source = null;
            _bus = null;
            ResetPresentation();
        }

        /// <summary>Clear transient resources before a consumer FSM reset when all effects must stop.</summary>
        public void ResetPresentation()
        {
            if (_resetting) return;
            _resetting = true;
            _revision++;
            try
            {
                _cleanup.Clear();
                _cleanup.AddRange(_executed);
                _executed.Clear();
                foreach (var cue in _cleanup)
                    if (cue != null)
                        try { cue.Stop(this); }
                        catch (Exception exception) { Debug.LogException(exception, cue); }
                _cleanup.Clear();
                if (audioSource != null) audioSource.Stop();
                foreach (var pool in particlePools) if (pool != null) pool.Clear(this);
                if (afterimage != null) afterimage.Clear();
                if (spriteFlash != null) spriteFlash.Clear();
                if (hitStop != null) hitStop.Cancel(this);
            }
            finally { _resetting = false; }
        }

        private void BuildBindings()
        {
            _bindings.Clear();
            _canonicalIds.Clear();
            _triggerController = null;
            if (animator != null && animator.runtimeAnimatorController != null) HasAnimatorTrigger(animator, 0);
            if (profile == null) throw new InvalidOperationException("Assign a Presentation profile before binding.");
            if (profile.definition != null)
                foreach (var state in profile.definition.Definition.states)
                    if (state.mirror_of_id >= 0) _canonicalIds.Add(state.id, state.mirror_of_id);
            int cueCount = 0;
            foreach (var binding in profile.bindings)
            {
                if (binding == null || !Enum.IsDefined(typeof(PresentationEvent), binding.trigger))
                    throw new InvalidOperationException("Invalid Presentation binding.");
                if (binding.trigger == PresentationEvent.DomainEvent && string.IsNullOrWhiteSpace(binding.eventKey))
                    throw new InvalidOperationException("Domain binding requires an exact event key.");
                if (binding.trigger != PresentationEvent.DomainEvent && (binding.stateId < 0 ||
                    (binding.trigger == PresentationEvent.Transition && binding.toStateId < 0)))
                    throw new InvalidOperationException("State bindings require nonnegative state IDs.");
                var key = Key(binding.trigger, binding.layerId, Canonical(binding.stateId),
                    Canonical(binding.toStateId), binding.eventKey);
                if (!_bindings.TryGetValue(key, out var actions)) _bindings.Add(key, actions = new List<(PresentationCue, bool)>());
                foreach (var cue in binding.actions)
                {
                    if (cue == null) throw new InvalidOperationException("Presentation binding contains a missing cue.");
                    actions.Add((cue, cue.GetType().Assembly == typeof(PresentationCue).Assembly));
                    cueCount++;
                }
            }
            _executed.EnsureCapacity(cueCount);
            if (_cleanup.Capacity < cueCount) _cleanup.Capacity = cueCount;
        }

        private int Canonical(int id) => _canonicalIds.TryGetValue(id, out int origin) ? origin : id;

        internal bool HasAnimatorTrigger(Animator target, int hash)
        {
            if (_triggerController != target.runtimeAnimatorController)
            {
                _triggerHashes.Clear();
                for (int i = 0; i < target.parameterCount; i++)
                {
                    var parameter = target.GetParameter(i);
                    if (parameter.type == AnimatorControllerParameterType.Trigger) _triggerHashes.Add(parameter.nameHash);
                }
                _triggerController = target.runtimeAnimatorController;
            }
            return _triggerHashes.Contains(hash);
        }

        private void OnStateChanged(UniversalFsmLayerStateChange change)
        {
            int revision = _revision;
            int from = Canonical(change.FromStateId), to = Canonical(change.ToStateId);
            foreach (var pool in particlePools) if (pool != null) pool.ClearState(this, change.LayerId, from);
            if (from >= 0) Dispatch(PresentationEvent.StateExit,
                UniversalFsmSignal.LayerExited(change.LayerId, change.LayerKey, from, string.Empty));
            if (revision != _revision) return;
            if (from >= 0 && to >= 0) Dispatch(PresentationEvent.Transition,
                UniversalFsmSignal.LayerTransition(change.LayerId, change.LayerKey, from, string.Empty, to, string.Empty));
            if (revision != _revision) return;
            if (to >= 0) Dispatch(PresentationEvent.StateEnter,
                UniversalFsmSignal.LayerEntered(change.LayerId, change.LayerKey, to, string.Empty));
        }

        private void OnDomainSignal(UniversalFsmSignal signal)
        {
            if (signal.Kind == UniversalFsmSignalKind.DomainEvent) Dispatch(PresentationEvent.DomainEvent, signal);
        }

        private void Dispatch(PresentationEvent trigger, in UniversalFsmSignal signal)
        {
            if (_resetting || !isActiveAndEnabled) return;
            using var marker = DispatchMarker.Auto();
            var key = Key(trigger, signal.LayerId,
                trigger == PresentationEvent.Transition ? signal.FromStateId : signal.StateId,
                signal.ToStateId, signal.EventKey);
            if (!_bindings.TryGetValue(key, out var actions)) return;
            int revision = _revision;
            for (int i = 0; i < actions.Count && revision == _revision && isActiveAndEnabled; i++)
            {
                var (cue, builtIn) = actions[i];
                _executed.Add(cue);
                if (builtIn) cue.Execute(this, signal);
                else
                    try { cue.Execute(this, signal); }
                    catch (Exception exception) { Debug.LogException(exception, cue); }
            }
        }

        private static (PresentationEvent, int, int, int, string) Key(PresentationEvent trigger,
            int layer, int state, int to, string eventKey) => trigger == PresentationEvent.DomainEvent
                ? (trigger, 0, 0, 0, eventKey)
                : (trigger, layer, state, trigger == PresentationEvent.Transition ? to : 0, string.Empty);
    }
}
