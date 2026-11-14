#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using OblivioMentis.UniversalFSM;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OblivioMentis.Presentation.Editor.Tests
{
    public sealed class HitStopSerializationTests
    {
        [Test]
        public void PackageResolutionHasNoErrorsAndKeepsUniversalFsmDependency()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HitStopCue).Assembly);
            Assert.NotNull(package); Assert.AreEqual("com.obliviomentis.presentation", package.name);
            Assert.AreEqual("0.3.2", package.version);
            Assert.IsEmpty(package.errors);
            Assert.AreEqual(1, package.dependencies.Length);
            Assert.AreEqual("com.obliviomentis.universalfsm", package.dependencies[0].name);
            Assert.AreEqual("0.2.0", package.dependencies[0].version);
            var fsm = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UniversalFsmSignal).Assembly);
            Assert.NotNull(fsm); Assert.IsEmpty(fsm.errors); Assert.AreEqual("0.2.0", fsm.version);
            foreach (var reference in typeof(HitStopCue).Assembly.GetReferencedAssemblies())
                StringAssert.DoesNotContain("Combat", reference.Name);
        }

        [Test]
        public void Legacy031YamlLoadsCleanAndRetainsAuthoredBehaviorBeforeExplicitRoundTrip()
        {
            string folder = "Assets/HitStopCompatibility_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            string path = folder + "/Legacy.asset";
            var owner = new GameObject("Legacy cue owner");
            float scale = Time.timeScale;
            try
            {
                const string guid = "3a3cb73c8d794690820c7f873d43a341";
                string yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                    "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" +
                    "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                    "  m_Script: {fileID: 11500000, guid: " + guid + ", type: 3}\n  m_Name: Legacy\n  m_EditorClassIdentifier: \n  scale: 0.08\n  duration: 0.12\n";
                File.WriteAllText(path, yaml); var before = File.ReadAllBytes(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var cue = AssetDatabase.LoadAssetAtPath<HitStopCue>(path);
                Assert.NotNull(cue); Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(cue))));
                Assert.AreEqual(.08f, cue.scale); Assert.AreEqual(.12f, cue.duration); Assert.AreEqual(HitStopDurationSource.Authored, cue.durationSource);
                Assert.AreEqual(1, cue.payloadDurationMultiplier); Assert.AreEqual(0, cue.minimumPayloadDuration); Assert.AreEqual(.2f, cue.maximumPayloadDuration);
                var driver = owner.AddComponent<FsmPresentationDriver>(); driver.hitStop = owner.AddComponent<PresentationHitStop>();
                var profile = ScriptableObject.CreateInstance<FsmPresentationProfile>();
                try
                {
                    driver.profile = profile; profile.bindings = new[] { new PresentationBinding { trigger = PresentationEvent.DomainEvent, eventKey = "hit", actions = new PresentationCue[] { cue } } };
                    FsmPresentationDriverEditor.Validate(driver);
                    Assert.IsFalse(EditorUtility.IsDirty(cue)); AssetDatabase.SaveAssets();
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "Load and setup validation must not rewrite legacy YAML.");
                    cue.Execute(driver, UniversalFsmSignal.Domain("hit", domainPayload: new UniversalFsmDomainPayload(duration: .001f)));
                    Assert.AreEqual(.08f, Time.timeScale); Assert.AreEqual(1, driver.hitStop.ActiveCount);
                    driver.hitStop.Cancel(driver);
                    using (var serialized = new SerializedObject(cue))
                    {
                        foreach (string field in new[] { "scale", "duration", "durationSource", "payloadDurationMultiplier", "minimumPayloadDuration", "maximumPayloadDuration" })
                            Assert.NotNull(serialized.FindProperty(field), field + " must be editable by the existing generic inline inspector.");
                        serialized.FindProperty("durationSource").enumValueIndex = 1;
                        serialized.FindProperty("payloadDurationMultiplier").floatValue = 2;
                        serialized.ApplyModifiedProperties();
                    }
                    AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var roundtrip = AssetDatabase.LoadAssetAtPath<HitStopCue>(path);
                    Assert.AreEqual(HitStopDurationSource.SignalPayload, roundtrip.durationSource); Assert.AreEqual(2, roundtrip.payloadDurationMultiplier);
                    Assert.AreEqual(.08f, roundtrip.scale); Assert.AreEqual(.12f, roundtrip.duration);
                    StringAssert.Contains("durationSource: 1", File.ReadAllText(path));
                    CollectionAssert.AreNotEqual(before, File.ReadAllBytes(path), "Intentional save adds new fields; byte identity is not claimed.");
                }
                finally { Object.DestroyImmediate(profile); }
            }
            finally { Object.DestroyImmediate(owner); Time.timeScale = scale; AssetDatabase.DeleteAsset(folder); }
        }
    }
}
