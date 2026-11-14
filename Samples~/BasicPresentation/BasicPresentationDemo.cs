#nullable enable
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation.Samples
{
    [DefaultExecutionOrder(-100)]
    public sealed class BasicPresentationDemo : MonoBehaviour
    {
        public FsmPresentationDriver driver = null!;
        public UniversalFsmRuntimeController controller = null!;
        private readonly UniversalFsmSignalBus _events = new();
        private bool _started;
        private bool _driverWasEnabled;
        private void Start()
        {
            // Пример сам управляет привязкой, поскольку передаёт собственную шину.
            // Ссылка нужна в редакторе для проверки настройки. Во время игры
            // отключаем автоматическую привязку, чтобы не повторять эффекты входа.
            driver.controller = null;
            _started = true;
            if (driver.isActiveAndEnabled) Rebind();
        }
        private void OnEnable()
        {
            if (_started && driver != null && driver.isActiveAndEnabled) Rebind();
        }
        private void OnDisable()
        {
            if (_started && driver != null)
            {
                driver.Unbind();
                RestorePersistentVisuals();
                _driverWasEnabled = false;
            }
        }
        public void Rebind()
        {
            driver.Bind((IUniversalFsmStateChangeSource)controller.Runtime!, _events);
            _driverWasEnabled = true;
        }
        private void LateUpdate()
        {
            // Привязка и очистка выполняются только при изменении активности.
            if (!_started || driver == null || _driverWasEnabled == driver.isActiveAndEnabled) return;
            if (driver.isActiveAndEnabled) Rebind();
            else { RestorePersistentVisuals(); _driverWasEnabled = false; }
        }
        private void RestorePersistentVisuals()
        {
            // Это правила примера: пакет не отменяет переключение объектов
            // и не управляет временем жизни смещения игровой камеры.
            if (driver.anchors != null)
                foreach (var entry in driver.anchors.entries)
                    if (entry.key == "weapon")
                        foreach (var renderer in entry.renderers)
                            if (renderer != null) renderer.enabled = false;
            if (driver.animator != null) driver.animator.ResetTrigger("Attack");
            if (driver.cameraSink is BasicCameraImpulse camera) camera.CancelImpulse();
        }
        public void Run() => controller.Runtime!.TryForceTransition("Main", 2);
        public void Idle() => controller.Runtime!.TryForceTransition("Main", 1);
        public void Jump() => controller.Runtime!.TryForceTransition("Main", 3);
        public void Attack() => controller.Runtime!.TryForceTransition("Main", 4);
        public void Damage() => _events.Publish(UniversalFsmSignal.Domain("Damage",
            domainPayload: new UniversalFsmDomainPayload(duration: 0.12f)));
        public void ResetDemo()
        {
            driver.ResetPresentation();
            RestorePersistentVisuals();
            controller.ResetRuntime();
        }
        public void TogglePresentation()
        {
            driver.enabled = !driver.enabled;
            if (driver.enabled) Rebind();
            else { RestorePersistentVisuals(); _driverWasEnabled = false; }
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 390, 310), GUI.skin.box);
            GUILayout.Label("UniversalFSM + Presentation 0.3.2");
            if (GUILayout.Button("Бег: анимация / звук / частицы")) Run();
            if (GUILayout.Button("Покой: очистка эффектов при выходе")) Idle();
            if (GUILayout.Button("Прыжок: анимация / остаточный силуэт")) Jump();
            if (GUILayout.Button("Атака: оружие / импульс камеры")) Attack();
            if (GUILayout.Button("Урон: вспышка / силуэт / замедление")) Damage();
            if (GUILayout.Button("Сбросить")) ResetDemo();
            if (GUILayout.Button("Отключить / включить эффекты")) TogglePresentation();
            GUILayout.Label("Пулы имеют фиксированный размер и видны в Hierarchy.");
            GUILayout.EndArea();
        }
    }
}
