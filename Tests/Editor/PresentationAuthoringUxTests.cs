#nullable enable
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OblivioMentis.UniversalFSM;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OblivioMentis.Presentation.Editor.Tests
{
    public sealed class PresentationAuthoringUxTests
    {
        private string _folder = "";
        private FsmPresentationProfile _profile = null!;
        private GameObject _owner = null!;
        private FsmPresentationDriver _driver = null!;
        private static UniversalStateDef State(int id = 3, int mirror = -1) => new() { id = id, mirror_of_id = mirror, layer_id = " Main ", name = "Run" };

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/AuthoringUxTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring(7));
            _profile = ScriptableObject.CreateInstance<FsmPresentationProfile>();
            AssetDatabase.CreateAsset(_profile, _folder + "/PlayerPresentation.asset");
            _owner = new GameObject("Authoring test");
            _driver = _owner.AddComponent<FsmPresentationDriver>();
            _driver.profile = _profile;
        }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_owner);
            Undo.ClearAll();
            AssetDatabase.DeleteAsset(_folder);
        }
        private PresentationCue Add(Type type, PresentationEvent trigger = PresentationEvent.StateEnter, int mirror = -1) =>
            PresentationAuthoring.CreateEffect(_profile, -1, PresentationAuthoring.StateBinding(State(mirror: mirror), trigger), type, "Run Enter - " + type.Name);

        [Test]
        public void OnlyProfileAppearsInPresentationCreateMenu()
        {
            var exposed = typeof(PresentationCue).Assembly.GetTypes().Where(t =>
                t.GetCustomAttribute<CreateAssetMenuAttribute>()?.menuName.StartsWith("FSM/Presentation/", StringComparison.Ordinal) == true).ToArray();
            CollectionAssert.AreEqual(new[] { typeof(FsmPresentationProfile) }, exposed);
            Assert.IsTrue(PresentationAuthoring.Effects.All(e => e.type.IsPublic));
        }
        [TestCase(PresentationEvent.StateEnter)] [TestCase(PresentationEvent.StateExit)]
        public void FirstEffectCreatesExactBindingAndStandaloneSiblingAsset(PresentationEvent trigger)
        {
            var cue = Add(typeof(AudioCue), trigger);
            var binding = _profile.bindings.Single();
            Assert.AreEqual(trigger, binding.trigger);
            Assert.AreEqual(3, binding.stateId);
            Assert.AreEqual(UniversalFsmBlackboard.StableHash("Main"), binding.layerId);
            Assert.AreSame(cue, binding.actions.Single());
            StringAssert.StartsWith(_folder + "/PlayerPresentation Cues/Run Enter - ", AssetDatabase.GetAssetPath(cue));
            Assert.IsTrue(AssetDatabase.IsMainAsset(cue));
            Assert.AreEqual(1, AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_profile)).Length);
        }
        [Test]
        public void MirrorUsesCanonicalStateAndKeepsMirrorLayer()
        {
            Add(typeof(SpriteFlashCue), mirror: 1);
            Assert.AreEqual(1, _profile.bindings.Single().stateId);
            Assert.AreEqual(UniversalFsmBlackboard.StableHash("Main"), _profile.bindings[0].layerId);
        }
        [Test]
        public void AdditionalEffectsReuseExactBindingAndUniqueAssetPaths()
        {
            var first = Add(typeof(AudioCue)); var second = Add(typeof(AudioCue));
            Assert.AreEqual(1, _profile.bindings.Length);
            CollectionAssert.AreEqual(new[] { first, second }, _profile.bindings[0].actions);
            Assert.AreNotEqual(AssetDatabase.GetAssetPath(first), AssetDatabase.GetAssetPath(second));
        }
        [Test]
        public void NativeReorderPersistsAndReferenceRemovalKeepsSharedAssets()
        {
            var first = Add(typeof(AudioCue)); var second = Add(typeof(SpriteFlashCue));
            PresentationAuthoring.AddReference(_profile, -1, PresentationAuthoring.StateBinding(State(7), PresentationEvent.StateExit), first);
            using var data = new SerializedObject(_profile);
            var actions = data.FindProperty("bindings").GetArrayElementAtIndex(0).FindPropertyRelative("actions");
            actions.MoveArrayElement(0, 1); data.ApplyModifiedProperties(); AssetDatabase.SaveAssets();
            string path = AssetDatabase.GetAssetPath(_profile);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            CollectionAssert.AreEqual(new[] { second, first }, AssetDatabase.LoadAssetAtPath<FsmPresentationProfile>(path).bindings[0].actions);
            data.Update(); actions = data.FindProperty("bindings").GetArrayElementAtIndex(0).FindPropertyRelative("actions");
            actions.GetArrayElementAtIndex(1).objectReferenceValue = null; actions.DeleteArrayElementAtIndex(1); data.ApplyModifiedProperties();
            Assert.AreSame(first, AssetDatabase.LoadAssetAtPath<AudioCue>(AssetDatabase.GetAssetPath(first)));
            Assert.AreSame(first, _profile.bindings[1].actions.Single());
            Assert.AreEqual(1, _profile.bindings[0].actions.Length);
        }
        [Test]
        public void CreateEffectUndoRedoRestoresBindingAndCueReference()
        {
            Add(typeof(AudioCue));
            Undo.PerformUndo(); Assert.AreEqual(0, _profile.bindings.Length);
            Undo.PerformRedo(); Assert.AreEqual(1, _profile.bindings.Length);
            Assert.IsNotNull(_profile.bindings[0].actions.Single());
            Assert.IsTrue(AssetDatabase.IsMainAsset(_profile.bindings[0].actions[0]));
        }
        [Test]
        public void ReuseReferenceUndoRedoRestoresBothStates()
        {
            var cue = Add(typeof(AudioCue)); Undo.IncrementCurrentGroup();
            PresentationAuthoring.AddReference(_profile, -1, PresentationAuthoring.StateBinding(State(7), PresentationEvent.StateExit), cue);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.AreEqual(1, _profile.bindings.Length);
            Undo.PerformRedo(); Assert.AreSame(cue, _profile.bindings[1].actions.Single());
        }
        [Test]
        public void TransitionSelectionAndDomainKeyPreserveExactIdentity()
        {
            var transition = PresentationAuthoring.StateBinding(State(), PresentationEvent.Transition, State(9, 6));
            int index = PresentationAuthoring.AppendBinding(_profile, transition);
            var cue = Add(typeof(AudioCue)); PresentationAuthoring.AddReference(_profile, index, transition, cue);
            Assert.AreEqual(3, _profile.bindings[0].stateId); Assert.AreEqual(6, _profile.bindings[0].toStateId);
            var domain = new PresentationBinding { trigger = PresentationEvent.DomainEvent, eventKey = " Damage " };
            PresentationAuthoring.AddReference(_profile, -1, domain, cue);
            Assert.AreEqual(" Damage ", _profile.bindings[2].eventKey);
            Assert.IsFalse(PresentationAuthoring.Matches(domain, new PresentationBinding { trigger = PresentationEvent.DomainEvent, eventKey = " damage " }));
        }
        [Test]
        public void AutoAssignUsesLocalComponentsAndLeavesSharedConcernsExplicit()
        {
            var outside = new GameObject("Unrelated actor");
            try
            {
                outside.AddComponent<AudioSource>(); outside.AddComponent<PresentationHitStop>();
                var child = new GameObject("Child Animator"); child.transform.parent = _owner.transform;
                var animator = child.AddComponent<Animator>();
                var controller = _owner.AddComponent<UniversalFsmRuntimeController>();
                var anchors = _owner.AddComponent<PresentationAnchorRegistry>();
                var afterimage = _owner.AddComponent<PresentationAfterimage>();
                var flash = _owner.AddComponent<PresentationSpriteFlash>();
                var pool = _owner.AddComponent<PresentationParticlePool>();
                FsmPresentationDriverEditor.AutoAssign(_driver);
                Assert.AreSame(controller, _driver.controller); Assert.AreSame(animator, _driver.animator);
                Assert.AreSame(anchors, _driver.anchors); CollectionAssert.AreEqual(new[] { pool }, _driver.particlePools);
                Assert.AreSame(afterimage, _driver.afterimage); Assert.AreSame(flash, _driver.spriteFlash);
                Assert.IsNull(_driver.audioSource); Assert.IsNull(_driver.hitStop); Assert.IsNull(_driver.cameraSink);
                var audio = _owner.AddComponent<AudioSource>(); FsmPresentationDriverEditor.AutoAssign(_driver);
                Assert.AreSame(audio, _driver.audioSource); Assert.AreEqual(1, _driver.particlePools.Length);
            }
            finally { Object.DestroyImmediate(outside); }
        }
        [Test]
        public void AmbiguousChildAnimatorsAreNotChosenAndAssignedReferencesArePreserved()
        {
            foreach (string name in new[] { "A", "B" }) { var child = new GameObject(name); child.transform.parent = _owner.transform; child.AddComponent<Animator>(); }
            FsmPresentationDriverEditor.AutoAssign(_driver); Assert.IsNull(_driver.animator);
            _driver.animator = _owner.GetComponentsInChildren<Animator>()[1];
            var chosen = _driver.animator; FsmPresentationDriverEditor.AutoAssign(_driver); Assert.AreSame(chosen, _driver.animator);
        }
        [Test]
        public void ValidationReportsRequirementsButDoesNotDemandUnusedEffects()
        {
            var empty = FsmPresentationDriverEditor.Validate(_driver);
            Assert.IsFalse(empty.Any(m => m.Contains("Audio") || m.Contains("Afterimage") || m.Contains("Hit Stop")));
            foreach (var effect in PresentationAuthoring.Effects) Add(effect.type);
            _profile.bindings[0].actions = _profile.bindings[0].actions.Concat(new PresentationCue[] { null! }).ToArray();
            var messages = string.Join("\n", FsmPresentationDriverEditor.Validate(_driver));
            foreach (string requirement in new[] { "controller", "Animator", "AudioSource", "PresentationAfterimage", "PresentationSpriteFlash", "PresentationHitStop", "IUniversalFsmCameraImpulseSink", "Missing cue", "Particle cue has no prefab", "PresentationAnchorRegistry" })
                StringAssert.Contains(requirement, messages);
        }
        [Test]
        public void ValidationDetectsMismatchedDefinitionAndMissingProfile()
        {
            _driver.profile = null;
            CollectionAssert.Contains(FsmPresentationDriverEditor.Validate(_driver), "Missing profile.");
            _driver.profile = _profile;
            var definition = ScriptableObject.CreateInstance<UniversalFsmDefinitionAsset>();
            AssetDatabase.CreateAsset(definition, _folder + "/Definition.asset");
            _profile.definition = definition;
            _driver.controller = _owner.AddComponent<UniversalFsmRuntimeController>();
            CollectionAssert.Contains(FsmPresentationDriverEditor.Validate(_driver), "Profile references another FSM definition.");
            using var controller = new SerializedObject(_driver.controller);
            controller.FindProperty("_fsmAsset").objectReferenceValue = definition; controller.ApplyModifiedProperties();
            Assert.IsEmpty(FsmPresentationDriverEditor.Validate(_driver));
        }
        [Test]
        public void AddPoolAssignsPrefabWithoutInstantiatingAndFindsDuplicates()
        {
            var particleObject = new GameObject("Burst");
            try
            {
                var particle = particleObject.AddComponent<ParticleSystem>(); var main = particle.main; main.loop = false;
                var prefab = PrefabUtility.SaveAsPrefabAsset(particleObject, _folder + "/Burst.prefab").GetComponent<ParticleSystem>();
                var cue = (ParticleCue)Add(typeof(ParticleCue)); cue.prefab = prefab;
                CollectionAssert.AreEqual(new[] { prefab }, FsmPresentationDriverEditor.MissingPools(_driver));
                int childCount = _owner.transform.childCount;
                var pool = FsmPresentationDriverEditor.AddPool(_driver, prefab);
                Assert.AreSame(prefab, pool.Prefab); Assert.AreSame(pool, _driver.particlePools.Single());
                Assert.AreEqual(8, new SerializedObject(pool).FindProperty("_capacity").intValue);
                Assert.AreEqual(childCount, _owner.transform.childCount); Assert.AreEqual(0, pool.Capacity);
                Assert.IsEmpty(FsmPresentationDriverEditor.MissingPools(_driver));
                Assert.AreSame(pool, FsmPresentationDriverEditor.AddPool(_driver, prefab));
                _driver.particlePools = new[] { pool, pool };
                Assert.IsTrue(FsmPresentationDriverEditor.Validate(_driver).Any(m => m.Contains("Duplicate particle pools")));
            }
            finally { Object.DestroyImmediate(particleObject); }
        }
    }
}
