#nullable enable
using System;
using UnityEngine;

namespace OblivioMentis.Presentation
{
    public sealed class PresentationAnchorRegistry : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string key = string.Empty;
            public Transform? target;
            public Renderer[] renderers = Array.Empty<Renderer>();
        }

        public Entry[] entries = Array.Empty<Entry>();

        public Entry Resolve(string key)
        {
            Entry? result = null;
            foreach (var entry in entries)
                if (entry.key == key)
                {
                    if (result != null) throw new InvalidOperationException($"Duplicate anchor '{key}'.");
                    result = entry;
                }
            return result ?? throw new InvalidOperationException($"Missing anchor '{key}'.");
        }
    }
}
