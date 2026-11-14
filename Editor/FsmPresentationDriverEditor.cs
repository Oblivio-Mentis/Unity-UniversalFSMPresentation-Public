#nullable enable
using System.Collections.Generic;
using System.Linq;
using OblivioMentis.UniversalFSM;
using UnityEditor;
using UnityEngine;

namespace OblivioMentis.Presentation.Editor
{
    [CustomEditor(typeof(FsmPresentationDriver))]
    internal sealed class FsmPresentationDriverEditor : UnityEditor.Editor
    {
        private bool _validate;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var driver = (FsmPresentationDriver)target;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Auto Assign Local Components")) { AutoAssign(driver); _validate = true; }
                if (GUILayout.Button("Validate Setup")) _validate = true;
                if (_validate)
                {
                    var messages = Validate(driver);
                    if (messages.Count == 0) EditorGUILayout.HelpBox("Setup references are valid.", MessageType.Info);
                    foreach (string message in messages) EditorGUILayout.HelpBox(message, MessageType.Warning);
                    foreach (var prefab in MissingPools(driver))
                        if (GUILayout.Button("Add Pool: " + prefab.name)) AddPool(driver, prefab);
                }
            }
        }

        internal static void AutoAssign(FsmPresentationDriver driver)
        {
            using var data = new SerializedObject(driver);
            void Assign(string name, Object? value)
            {
                var field = data.FindProperty(name);
                if (field.objectReferenceValue == null && value != null) field.objectReferenceValue = value;
            }
            Assign("controller", driver.GetComponent<UniversalFsmRuntimeController>());
            var animator = driver.GetComponent<Animator>();
            var children = driver.GetComponentsInChildren<Animator>(true);
            // Ambiguous children need an explicit choice. Preserve authored references.
            Assign("animator", animator != null ? animator : children.Length == 1 ? children[0] : null);
            Assign("audioSource", driver.GetComponent<AudioSource>());
            Assign("anchors", driver.GetComponent<PresentationAnchorRegistry>());
            Assign("afterimage", driver.GetComponent<PresentationAfterimage>());
            Assign("spriteFlash", driver.GetComponent<PresentationSpriteFlash>());
            var pools = data.FindProperty("particlePools");
            foreach (var pool in driver.GetComponents<PresentationParticlePool>())
            {
                bool found = false;
                for (int i = 0; i < pools.arraySize; i++) if (pools.GetArrayElementAtIndex(i).objectReferenceValue == pool) found = true;
                if (!found) pools.GetArrayElementAtIndex(pools.arraySize++).objectReferenceValue = pool;
            }
            data.ApplyModifiedProperties();
        }

        private static IEnumerable<PresentationCue?> Cues(FsmPresentationDriver driver) => driver.profile == null
            ? System.Array.Empty<PresentationCue>()
            : driver.profile.bindings.Where(b => b != null).SelectMany(b => b.actions);

        internal static List<ParticleSystem> MissingPools(FsmPresentationDriver driver) => Cues(driver).OfType<ParticleCue>()
            .Where(c => c.prefab != null && !driver.particlePools.Any(p => p != null && p.Prefab == c.prefab))
            .Select(c => c.prefab!).Distinct().ToList();

        internal static List<string> Validate(FsmPresentationDriver driver)
        {
            var messages = new List<string>();
            void Warn(bool condition, string message) { if (condition && !messages.Contains(message)) messages.Add(message); }
            Warn(driver.controller == null, "Missing controller.");
            Warn(driver.profile == null, "Missing profile.");
            if (driver.profile == null) return messages;
            Warn(driver.profile.definition == null, "Profile has no FSM definition.");
            Warn(driver.controller != null && driver.profile.definition != driver.controller.DefinitionAsset, "Profile references another FSM definition.");
            foreach (var binding in driver.profile.bindings)
            {
                Warn(binding == null, "Missing binding.");
                if (binding != null) Warn(binding.trigger == PresentationEvent.DomainEvent && string.IsNullOrWhiteSpace(binding.eventKey), "Domain event requires a nonempty exact key.");
            }
            foreach (var cue in Cues(driver))
            {
                Warn(cue == null, "Missing cue reference.");
                Warn((cue is AnimatorStateCue || cue is AnimatorTriggerCue) && driver.animator == null, "Animator cue requires an Animator.");
                Warn((cue is AnimatorStateCue || cue is AnimatorTriggerCue) && driver.animator != null && driver.animator.runtimeAnimatorController == null, "Animator has no runtime controller.");
                Warn(cue is AudioCue && driver.audioSource == null, "Audio cue requires an AudioSource.");
                Warn(cue is AudioCue audio && audio.clip == null, "Audio cue has no clip.");
                Warn(cue is AfterimageCue && driver.afterimage == null, "Afterimage cue requires PresentationAfterimage.");
                Warn(cue is SpriteFlashCue && driver.spriteFlash == null, "Sprite Flash cue requires PresentationSpriteFlash.");
                Warn(cue is HitStopCue && driver.hitStop == null, "Hit Stop cue requires an explicit shared PresentationHitStop.");
                Warn(cue is CameraImpulseCue && driver.cameraSink is not IUniversalFsmCameraImpulseSink, "Camera sink must implement IUniversalFsmCameraImpulseSink.");
                Warn((cue is AttachmentCue || cue is ParticleCue anchored && !string.IsNullOrEmpty(anchored.anchor)) && driver.anchors == null, "Anchored cue requires PresentationAnchorRegistry.");
                Warn(cue is ParticleCue particle && particle.prefab == null, "Particle cue has no prefab.");
            }
            foreach (var prefab in MissingPools(driver)) messages.Add("Missing pool for: " + prefab.name);
            var prefabs = new HashSet<ParticleSystem>();
            foreach (var pool in driver.particlePools)
            {
                Warn(pool == null, "Missing particle pool reference.");
                if (pool == null) continue;
                Warn(pool.Prefab == null, "Particle pool has no prefab.");
                if (pool.Prefab != null)
                {
                    Warn(!prefabs.Add(pool.Prefab), "Duplicate particle pools for: " + pool.Prefab.name);
                    Warn(pool.Prefab.main.loop || pool.Prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 1 || pool.Prefab.subEmitters.subEmittersCount != 0,
                        "Pool prefab must contain one non-looping ParticleSystem without subemitters: " + pool.Prefab.name);
                }
            }
            return messages;
        }

        internal static PresentationParticlePool AddPool(FsmPresentationDriver driver, ParticleSystem prefab)
        {
            var existing = driver.particlePools.FirstOrDefault(p => p != null && p.Prefab == prefab);
            if (existing != null) return existing;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Presentation particle pool");
            var pool = driver.GetComponents<PresentationParticlePool>().FirstOrDefault(p => p.Prefab == prefab)
                ?? Undo.AddComponent<PresentationParticlePool>(driver.gameObject);
            using var data = new SerializedObject(pool);
            data.FindProperty("_prefab").objectReferenceValue = prefab;
            data.ApplyModifiedProperties();
            using var owner = new SerializedObject(driver);
            var pools = owner.FindProperty("particlePools");
            pools.GetArrayElementAtIndex(pools.arraySize++).objectReferenceValue = pool;
            owner.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
            return pool;
        }
    }
}
