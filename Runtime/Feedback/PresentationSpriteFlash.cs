#nullable enable
using System;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class PresentationSpriteFlash : MonoBehaviour
    {
        public SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        private Color[] _original = Array.Empty<Color>();
        private Color _written;
        private float _end;
        private bool _active;

        private void Awake() => enabled = false;

        public void Flash(Color color, float duration)
        {
            if (!float.IsFinite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (_original.Length != renderers.Length)
            {
                if (_active) throw new InvalidOperationException("Do not change flash renderer configuration while active.");
                _original = new Color[renderers.Length];
            }
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                {
                    if (!_active) _original[i] = renderers[i].color;
                    renderers[i].color = color;
                }
            _written = color;
            _end = Time.unscaledTime + duration;
            _active = true;
            enabled = true;
        }

        private void Update() { if (Time.unscaledTime >= _end) Clear(); }

        public void Clear()
        {
            if (_active)
                for (int i = 0; i < renderers.Length && i < _original.Length; i++)
                    if (renderers[i] != null && renderers[i].color == _written) renderers[i].color = _original[i];
            _active = false;
            enabled = false;
        }

        private void OnDisable() => Clear();
    }
}
