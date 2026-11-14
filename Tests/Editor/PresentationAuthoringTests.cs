#nullable enable
using System;
using NUnit.Framework;
using OblivioMentis.UniversalFSM;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OblivioMentis.Presentation.Editor.Tests
{
    public sealed class PresentationAuthoringTests
    {
        private string _folder = string.Empty;
        private GameObject _owner = null!;
        private FsmPresentationDriver _driver = null!;
        private AnimatorController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/PresentationTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring(7));
            _owner = new GameObject("Animator tests"); _driver = _owner.AddComponent<FsmPresentationDriver>();
            _driver.animator = _owner.AddComponent<Animator>();
            _controller = AnimatorController.CreateAnimatorControllerAtPath(_folder + "/Test.controller");
            var machine = _controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle"); var run = machine.AddState("Run"); machine.defaultState = idle;
            var clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, _folder + "/Motion.anim"); idle.motion = run.motion = clip;
            _controller.AddParameter("Go", AnimatorControllerParameterType.Trigger);
            var transition = idle.AddTransition(run); transition.hasExitTime = false; transition.duration = 0;
            transition.AddCondition(AnimatorConditionMode.If, 0, "Go");
            _driver.animator.runtimeAnimatorController = _controller;
            _driver.animator.Rebind(); _driver.animator.Update(0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_owner); Undo.ClearAll(); AssetDatabase.DeleteAsset(_folder);
        }

        private T Cue<T>() where T : ScriptableObject
        {
            var cue = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(cue, _folder + "/" + typeof(T).Name + ".asset"); return cue;
        }

        [Test]
        public void AnimatorStateCueReachesNativeState()
        {
            var cue = Cue<AnimatorStateCue>(); cue.state = "Base Layer.Run"; cue.fadeSeconds = 0;
            cue.Execute(_driver, default); _driver.animator!.Update(0.02f);
            Assert.IsTrue(_driver.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Run"));
        }

        [Test]
        public void AnimatorTriggerCueReachesNativeTransition()
        {
            var cue = Cue<AnimatorTriggerCue>(); cue.parameter = "Go"; cue.Execute(_driver, default);
            _driver.animator!.Update(0.02f); _driver.animator.Update(0.02f);
            Assert.IsTrue(_driver.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Run"));
        }

        [Test]
        public void AnimatorResetTriggerPreventsTransition()
        {
            var cue = Cue<AnimatorTriggerCue>(); cue.parameter = "Go"; cue.Execute(_driver, default);
            cue.reset = true; cue.Execute(_driver, default); _driver.animator!.Update(0.02f);
            Assert.IsTrue(_driver.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"));
        }

        [TestCase(-1, 0.1f)] [TestCase(9, 0.1f)] [TestCase(0, -1)] [TestCase(0, float.NaN)]
        public void InvalidAnimatorLayerOrDurationFailsClearly(int layer, float duration)
        {
            var cue = Cue<AnimatorStateCue>(); cue.layer = layer; cue.fadeSeconds = duration;
            Assert.Throws<InvalidOperationException>(() => cue.Execute(_driver, default));
        }

        [Test]
        public void MissingAnimatorStateAndTriggerFailClearly()
        {
            var state = Cue<AnimatorStateCue>(); state.state = "Base Layer.Missing";
            var trigger = Cue<AnimatorTriggerCue>(); trigger.parameter = "Missing";
            Assert.Throws<InvalidOperationException>(() => state.Execute(_driver, default));
            Assert.Throws<InvalidOperationException>(() => trigger.Execute(_driver, default));
        }

        [Test]
        public void ProfileRoundTripKeepsOrderedStandaloneCueReferences()
        {
            var profile = Cue<FsmPresentationProfile>(); var audio = Cue<AudioCue>(); var state = Cue<AnimatorStateCue>();
            profile.bindings = new[] { new PresentationBinding { layerId = 12, stateId = 7, actions = new PresentationCue[] { audio, state } } };
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            var path = AssetDatabase.GetAssetPath(profile);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<FsmPresentationProfile>(path);
            Assert.AreEqual(7, loaded.bindings[0].stateId);
            CollectionAssert.AreEqual(new PresentationCue[] { audio, state }, loaded.bindings[0].actions);
            Assert.IsTrue(AssetDatabase.IsMainAsset(audio)); Assert.AreEqual(1, AssetDatabase.LoadAllAssetsAtPath(path).Length);
            string json = EditorJsonUtility.ToJson(loaded);
            StringAssert.DoesNotContain("schemaVersion", json); StringAssert.DoesNotContain("stateKey", json);
        }

        [Test]
        public void AddCanonicalStateBindingUsesUnityUndoRedo()
        {
            var profile = Cue<FsmPresentationProfile>();
            FsmPresentationProfileEditor.AddBinding(profile, new UniversalStateDef { id = 5, mirror_of_id = 2, layer_id = "Main" }, PresentationEvent.StateExit);
            Undo.FlushUndoRecordObjects(); Assert.AreEqual(2, profile.bindings[0].stateId);
            Assert.AreEqual(UniversalFsmBlackboard.StableHash("Main"), profile.bindings[0].layerId);
            Undo.PerformUndo(); Assert.AreEqual(0, profile.bindings.Length);
            Undo.PerformRedo(); Assert.AreEqual(1, profile.bindings.Length);
        }

        [Test]
        public void WorkspaceCreatesNativeInspectorWithoutStandaloneWindow()
        {
            var provider = new UniversalFsmPresentationWorkspaceProvider();
            using var workspace = provider.CreateWorkspace(new OblivioMentis.UniversalFSM.Editor.UniversalFsmEditorWorkspaceContext(_ => { }, _ => { }));
            var definition = Cue<UniversalFsmDefinitionAsset>(); workspace.SetAsset(definition); workspace.Refresh();
            Assert.NotNull(workspace.MainContent); Assert.Greater(workspace.Sidebar.childCount, 0);
        }

        [Test]
        public void RemovedRuntimeFrameworkTypesAreAbsent()
        {
            var assembly = typeof(FsmPresentationDriver).Assembly;
            foreach (var name in new[] { "UniversalFsmSignalRelay", "FsmPresentationDirector", "PresentationStateIdentity", "PresentationHandle", "IFsmPresentationServices", "FsmPresentationPresetLibrary", "AudioPresentationService" })
                Assert.IsNull(assembly.GetType("OblivioMentis.Presentation." + name), name);
        }
    }
}
