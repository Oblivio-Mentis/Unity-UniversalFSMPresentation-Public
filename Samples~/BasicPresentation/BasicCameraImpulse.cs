using OblivioMentis.Presentation;
using UnityEngine;

namespace OblivioMentis.Presentation.Samples
{
    // Адаптер для примера; игровая система камеры должна сама объединять смещения.
    public sealed class BasicCameraImpulse : MonoBehaviour, IUniversalFsmCameraImpulseSink
    {
        private Vector3 _origin;
        private float _end, _amplitude;
        private bool _active;

        public void EmitImpulse(float amplitude, float durationSeconds)
        {
            if (!_active) _origin = transform.localPosition;
            _amplitude = amplitude;
            _end = Time.unscaledTime + durationSeconds;
            _active = true;
        }

        private void LateUpdate()
        {
            if (!_active) return;
            if (Time.unscaledTime >= _end) { CancelImpulse(); return; }
            transform.localPosition = _origin + Vector3.right *
                (Mathf.Sin(Time.unscaledTime * 80) * _amplitude);
        }

        public void CancelImpulse()
        {
            if (_active) transform.localPosition = _origin;
            _active = false;
        }

        private void OnDisable() => CancelImpulse();
    }
}
