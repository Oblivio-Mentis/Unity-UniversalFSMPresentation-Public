#nullable enable
using System;
using System.IO;
using OblivioMentis.UniversalFSM;
using UnityEditor;
using UnityEngine;

namespace OblivioMentis.Presentation.Editor
{
    // Conveniences over the existing model; cues remain ordinary reusable assets.
    internal static class PresentationAuthoring
    {
        internal static readonly (string menu, Type type)[] Effects =
        {
            ("Animation/Animator State", typeof(AnimatorStateCue)),
            ("Animation/Animator Trigger", typeof(AnimatorTriggerCue)),
            ("Audio/Audio One Shot", typeof(AudioCue)),
            ("VFX/Particle One Shot", typeof(ParticleCue)),
            ("VFX/Afterimage", typeof(AfterimageCue)),
            ("VFX/Sprite Flash", typeof(SpriteFlashCue)),
            ("Feedback/Hit Stop", typeof(HitStopCue)),
            ("Feedback/Camera Impulse", typeof(CameraImpulseCue)),
            ("Object/Attachment", typeof(AttachmentCue))
        };
        internal static int Canonical(UniversalStateDef state) => state.mirror_of_id >= 0 ? state.mirror_of_id : state.id;
        internal static string Layer(UniversalStateDef state) => string.IsNullOrWhiteSpace(state.layer_id) ? "Default" : state.layer_id.Trim();
        internal static int LayerId(UniversalStateDef state) => UniversalFsmBlackboard.StableHash(Layer(state));
        internal static PresentationBinding StateBinding(UniversalStateDef state, PresentationEvent trigger, UniversalStateDef? to = null) =>
            new() { trigger = trigger, layerId = LayerId(state), stateId = Canonical(state), toStateId = to == null ? 0 : Canonical(to) };

        internal static int AppendBinding(FsmPresentationProfile profile, PresentationBinding binding)
        {
            Undo.RecordObject(profile, "Add Presentation binding");
            var bindings = profile.bindings;
            Array.Resize(ref bindings, bindings.Length + 1);
            bindings[^1] = binding;
            profile.bindings = bindings;
            EditorUtility.SetDirty(profile);
            return bindings.Length - 1;
        }
        internal static bool Matches(PresentationBinding binding, PresentationBinding key) =>
            binding.trigger == key.trigger && (key.trigger == PresentationEvent.DomainEvent
                ? string.Equals(binding.eventKey, key.eventKey, StringComparison.Ordinal)
                : binding.layerId == key.layerId && binding.stateId == key.stateId &&
                  (key.trigger != PresentationEvent.Transition || binding.toStateId == key.toStateId));

        internal static void AddReference(FsmPresentationProfile profile, int index, PresentationBinding key, PresentationCue cue)
        {
            if (index < 0)
            {
                index = Array.FindIndex(profile.bindings, b => b != null && Matches(b, key));
                if (index < 0) index = AppendBinding(profile, key);
            }
            using var serialized = new SerializedObject(profile);
            var actions = serialized.FindProperty("bindings").GetArrayElementAtIndex(index).FindPropertyRelative("actions");
            int end = actions.arraySize++;
            actions.GetArrayElementAtIndex(end).objectReferenceValue = cue;
            serialized.ApplyModifiedProperties();
        }
        internal static PresentationCue CreateEffect(FsmPresentationProfile profile, int index, PresentationBinding key, Type type, string label)
        {
            string path = AssetDatabase.GetAssetPath(profile);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("Save the profile under Assets before creating effects.");
            string directory = Path.GetDirectoryName(path)!.Replace('\\', '/');
            string folderName = Path.GetFileNameWithoutExtension(path) + " Cues";
            string folder = directory + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(directory, folderName);
            foreach (char character in Path.GetInvalidFileNameChars()) label = label.Replace(character, '_');
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Presentation effect");
            var cue = (PresentationCue)ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(cue, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + label + ".asset"));
            Undo.RegisterCreatedObjectUndo(cue, "Create Presentation cue");
            AddReference(profile, index, key, cue);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            return cue;
        }
    }
}
