#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    /// <summary>One scene-authored coordinator for global time. Uses unscaled expiry and ownership-aware restoration.</summary>
    public sealed class PresentationHitStop : MonoBehaviour
    {
        public bool scaleFixedDeltaTime;
        private static PresentationHitStop? _coordinator;
        private readonly List<Request> _requests = new();
        private float _baseline, _fixedBaseline, _written, _fixedWritten;
        private bool _ownsScale, _ownsFixed, _session, _paused;
        public int ActiveCount => _requests.Count;

        private struct Request
        {
            public FsmPresentationDriver Owner;
            public float Scale, End;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _coordinator = null;
        private void Awake() => enabled = false;

        public void RequestStop(FsmPresentationDriver owner, float scale, float duration)
        {
            if (owner == null || !owner.isActiveAndEnabled) return;
            if (!float.IsFinite(scale) || scale < 0 || scale > 1 || !float.IsFinite(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(scale), "Hit stop requires scale [0,1] and positive finite duration.");
            if (_coordinator != null && _coordinator != this)
                throw new InvalidOperationException("Assign the same hit-stop coordinator to all Presentation drivers.");
            if (_paused || (!_session && Time.timeScale == 0)) return;
            if (!_session)
            {
                _coordinator = this;
                _session = true;
                _baseline = _written = Time.timeScale;
                _fixedBaseline = _fixedWritten = Time.fixedDeltaTime;
                _ownsScale = true;
                _ownsFixed = scaleFixedDeltaTime;
            }
            _requests.Add(new Request { Owner = owner, Scale = scale, End = Time.unscaledTime + duration });
            enabled = true;
            Apply();
        }

        /// <summary>Pause integration: call before the pause owner writes Time.timeScale, including a same-value zero write.</summary>
        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            if (paused) Clear();
        }

        public void Cancel(FsmPresentationDriver owner)
        {
            for (int i = _requests.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_requests[i].Owner, owner)) _requests.RemoveAt(i);
            Apply();
        }

        private void Update()
        {
            for (int i = _requests.Count - 1; i >= 0; i--)
                if (_requests[i].Owner == null || !_requests[i].Owner.isActiveAndEnabled || Time.unscaledTime >= _requests[i].End)
                    _requests.RemoveAt(i);
            Apply();
        }

        private void ObserveExternalWrites()
        {
            if (Time.timeScale != _written) _ownsScale = false;
            if (Time.fixedDeltaTime != _fixedWritten) _ownsFixed = false;
        }

        private void Apply()
        {
            if (!_session) return;
            ObserveExternalWrites();
            if (_requests.Count == 0) { Clear(); return; }
            if (!_ownsScale)
            {
                if (_ownsFixed) { Time.fixedDeltaTime = _fixedBaseline; _ownsFixed = false; }
                return;
            }
            float scale = _baseline;
            foreach (var request in _requests) scale = Mathf.Min(scale, request.Scale);
            Time.timeScale = scale;
            _written = Time.timeScale;
            if (_ownsFixed)
            {
                Time.fixedDeltaTime = Mathf.Max(0.0001f, _fixedBaseline * scale / _baseline);
                // Unity stores a quantized native value; compare against what it actually accepted.
                _fixedWritten = Time.fixedDeltaTime;
            }
        }

        private void Clear()
        {
            if (_session)
            {
                ObserveExternalWrites();
                if (_ownsScale) Time.timeScale = _baseline;
                if (_ownsFixed) Time.fixedDeltaTime = _fixedBaseline;
            }
            _requests.Clear();
            _session = _ownsScale = _ownsFixed = false;
            if (_coordinator == this) _coordinator = null;
            enabled = false;
        }

        private void OnDisable() => Clear();
    }
}
