#nullable enable
using System;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class PresentationAfterimage : MonoBehaviour
    {
        public SpriteRenderer? source;
        [SerializeField, Min(1)] private int _capacity = 12;
        private Slot[] _slots = Array.Empty<Slot>();
        private GameObject? _root;
        public int ActiveCount { get; private set; }

        private struct Slot
        {
            public SpriteRenderer Renderer;
            public Color Tint;
            public float Start, Duration;
            public bool Active;
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_capacity < 1) throw new InvalidOperationException("Afterimage capacity must be positive.");
            _root = new GameObject($"Presentation afterimages ({_capacity})");
            // World-space snapshots must not follow their moving source.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_root, gameObject.scene);
            _slots = new Slot[_capacity];
            for (int i = 0; i < _slots.Length; i++)
            {
                var child = new GameObject("Afterimage");
                child.transform.SetParent(_root.transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.enabled = false;
                _slots[i].Renderer = renderer;
            }
            enabled = false;
        }

        public bool Snapshot(Color tint, float duration)
        {
            if (source == null || source.sprite == null || !float.IsFinite(duration) || duration <= 0)
                throw new InvalidOperationException("Afterimage requires a source sprite and positive finite duration.");
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var slot = ref _slots[i];
                if (slot.Active) continue;
                var target = slot.Renderer;
                target.sprite = source.sprite;
                target.sharedMaterial = source.sharedMaterial;
                target.sortingLayerID = source.sortingLayerID;
                target.sortingOrder = source.sortingOrder;
                target.flipX = source.flipX;
                target.flipY = source.flipY;
                target.drawMode = source.drawMode;
                target.size = source.size;
                target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                target.transform.localScale = source.transform.lossyScale;
                target.color = tint;
                target.enabled = true;
                slot.Tint = tint;
                slot.Start = Time.unscaledTime;
                slot.Duration = duration;
                slot.Active = true;
                ActiveCount++;
                enabled = true;
                return true;
            }
            return false;
        }

        private void Update()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var slot = ref _slots[i];
                if (!slot.Active) continue;
                float fraction = (Time.unscaledTime - slot.Start) / slot.Duration;
                var tint = slot.Tint;
                tint.a *= Mathf.Clamp01(1 - fraction);
                slot.Renderer.color = tint;
                if (fraction >= 1)
                {
                    slot.Renderer.enabled = false;
                    slot.Active = false;
                    ActiveCount--;
                }
            }
            if (ActiveCount == 0) enabled = false;
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Active = false;
                if (_slots[i].Renderer != null) _slots[i].Renderer.enabled = false;
            }
            ActiveCount = 0;
            enabled = false;
        }

        private void OnDisable() => Clear();
        private void OnDestroy() { if (_root != null) Destroy(_root); }
    }
}
