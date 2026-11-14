#nullable enable
using System;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    /// <summary>Fixed, visible pool for a single non-looping ParticleSystem prefab; drop newest on exhaustion.</summary>
    public sealed class PresentationParticlePool : MonoBehaviour
    {
        [SerializeField] private ParticleSystem? _prefab;
        [SerializeField, Min(1)] private int _capacity = 8;
        private PresentationParticleReturn[] _slots = Array.Empty<PresentationParticleReturn>();
        private Transform? _root;
        public ParticleSystem? Prefab => _prefab;
        public int Capacity => _slots.Length;
        public int ActiveCount { get; private set; }

        private void Awake() { if (_prefab != null) Initialize(); }

        /// <summary>Setup only; cannot replace a pool after initialization.</summary>
        public void Initialize(ParticleSystem? prefab = null, int capacity = 0)
        {
            if (_slots.Length != 0) throw new InvalidOperationException("Pool is already initialized.");
            if (prefab != null) _prefab = prefab;
            if (capacity != 0) _capacity = capacity;
            if (_prefab == null || _capacity < 1 || _prefab.main.loop ||
                _prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 1 || _prefab.subEmitters.subEmittersCount != 0)
                throw new InvalidOperationException("Pool requires one non-looping particle system, no subemitters, and positive capacity.");
            var root = new GameObject($"Presentation particles ({_capacity})");
            _root = root.transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            root.SetActive(false);
            _slots = new PresentationParticleReturn[_capacity];
            for (int i = 0; i < _slots.Length; i++)
            {
                var instance = Instantiate(_prefab, _root);
                instance.gameObject.SetActive(false);
                var main = instance.main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.Callback;
                var slot = instance.gameObject.AddComponent<PresentationParticleReturn>();
                slot.Pool = this;
                slot.Particles = instance;
                _slots[i] = slot;
            }
            root.SetActive(true);
        }

        public bool TryPlay(FsmPresentationDriver owner, Vector3 position, Quaternion rotation,
            int layerId = -1, int stateId = -1, bool stopOnStateExit = false)
        {
            if (_slots.Length == 0) throw new InvalidOperationException("Initialize particle pools before gameplay dispatch.");
            if (!isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled) return false;
            foreach (var slot in _slots)
            {
                if (slot.InUse) continue;
                slot.InUse = true;
                slot.Owner = owner;
                slot.Layer = layerId;
                slot.State = stateId;
                slot.StopOnExit = stopOnStateExit;
                ActiveCount++;
                slot.transform.SetPositionAndRotation(position, rotation);
                slot.gameObject.SetActive(true);
                slot.Particles.Play(false);
                return true;
            }
            return false;
        }

        internal void Release(PresentationParticleReturn slot)
        {
            if (!slot.InUse) return;
            slot.InUse = false;
            slot.Owner = null;
            ActiveCount--;
            slot.Particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            slot.gameObject.SetActive(false);
        }

        public void Clear(FsmPresentationDriver owner)
        {
            foreach (var slot in _slots) if (slot.Owner == owner) Release(slot);
        }

        internal void ClearState(FsmPresentationDriver owner, int layer, int state)
        {
            foreach (var slot in _slots)
                if (slot.Owner == owner && slot.StopOnExit && slot.Layer == layer && slot.State == state) Release(slot);
        }

        private void OnDisable() { foreach (var slot in _slots) if (slot != null) Release(slot); }
        private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
