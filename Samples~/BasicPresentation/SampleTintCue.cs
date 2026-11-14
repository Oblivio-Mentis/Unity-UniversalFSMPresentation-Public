using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation.Samples
{
    // Изменяемое состояние эффекта хранится на персонаже, а не в общем ассете.
    [CreateAssetMenu(menuName = "FSM/Presentation/Примеры/Оттенок спрайта")]
    public sealed class SampleTintCue : PresentationCue
    {
        public Color color = Color.yellow;
        [Min(0.001f)] public float duration = 0.15f;

        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            if (driver.spriteFlash != null) driver.spriteFlash.Flash(color, duration);
        }

        public override void Stop(FsmPresentationDriver driver)
        {
            if (driver.spriteFlash != null) driver.spriteFlash.Clear();
        }
    }
}
