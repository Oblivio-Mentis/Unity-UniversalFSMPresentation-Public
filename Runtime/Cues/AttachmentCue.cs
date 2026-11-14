#nullable enable
using System;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class AttachmentCue : PresentationCue
    {
        public string anchor = string.Empty;
        public bool visible = true;
        public bool renderersOnly = true;

        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            var entry = driver.anchors?.Resolve(anchor) ?? throw new InvalidOperationException("Attachment requires anchors.");
            if (renderersOnly)
            {
                foreach (var target in entry.renderers) if (target != null) target.enabled = visible;
            }
            else
            {
                if (entry.target == null) throw new InvalidOperationException("Attachment anchor has no target.");
                entry.target.gameObject.SetActive(visible);
            }
        }
    }
}
