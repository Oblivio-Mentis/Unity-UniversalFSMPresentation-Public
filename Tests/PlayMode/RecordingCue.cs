#nullable enable
using System;
using OblivioMentis.UniversalFSM;

namespace OblivioMentis.Presentation.PlayMode.Tests
{
    public sealed class RecordingCue : PresentationCue
    {
        public int calls, stops;
        [NonSerialized] public UniversalFsmSignal last;
        public Action<FsmPresentationDriver>? onExecute;
        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            calls++;
            last = signal;
            onExecute?.Invoke(driver);
        }
        public override void Stop(FsmPresentationDriver driver) => stops++;
    }
}
