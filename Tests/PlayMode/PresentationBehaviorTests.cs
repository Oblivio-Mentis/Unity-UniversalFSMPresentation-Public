#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OblivioMentis.UniversalFSM;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OblivioMentis.Presentation.PlayMode.Tests
{
    public sealed class PresentationBehaviorTests
    {
        private readonly List<Object> _objects = new();
        private FsmPresentationDriver _driver = null!;
        private FsmPresentationProfile _profile = null!;
        private Source _source = null!;
        private float _timeScale, _fixedDelta;

        private sealed class Source : IUniversalFsmStateChangeSource
        {
            public event Action<UniversalFsmLayerStateChange>? LayerStateChanged;
            public void Change(int from, int to, int layer = 7) => LayerStateChanged?.Invoke(new UniversalFsmLayerStateChange(layer, "Main", from, to));
        }

        private sealed class State : BaseUniversalFsmState<object>
        {
            private readonly int _id;
            public State(int id) => _id = id;
            public override int StateId => _id;
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); _objects.Add(asset); return asset;
        }
        private GameObject Go(string name)
        {
            var go = new GameObject(name); _objects.Add(go); return go;
        }

        [SetUp]
        public void SetUp()
        {
            _timeScale = Time.timeScale; _fixedDelta = Time.fixedDeltaTime;
            Time.timeScale = 1;
            _driver = Go("Presentation test owner").AddComponent<FsmPresentationDriver>();
            _profile = Asset<FsmPresentationProfile>();
            _driver.profile = _profile;
            _source = new Source();
        }

        [TearDown]
        public void TearDown()
        {
            if (_driver != null) _driver.Unbind();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Time.timeScale = _timeScale; Time.fixedDeltaTime = _fixedDelta;
        }

        private PresentationBinding Binding(PresentationCue cue, PresentationEvent trigger = PresentationEvent.StateEnter, int state = 2) =>
            new() { trigger = trigger, layerId = 7, stateId = state, toStateId = 2, eventKey = "Damage", actions = new[] { cue } };

        [Test]
        public void ExactStateAndLayerMatchOnly()
        {
            var cue = Asset<RecordingCue>(); _profile.bindings = new[] { Binding(cue) }; _driver.Bind(_source);
            _source.Change(0, 3); _source.Change(0, 2, 8); Assert.AreEqual(0, cue.calls);
            _source.Change(0, 2); Assert.AreEqual(1, cue.calls);
        }

        [Test]
        public void ExitTransitionEnterPreserveAuthoredOrder()
        {
            var order = new List<int>();
            var exit = Asset<RecordingCue>(); exit.onExecute = _ => order.Add(1);
            var transition = Asset<RecordingCue>(); transition.onExecute = _ => order.Add(2);
            var enter = Asset<RecordingCue>(); enter.onExecute = _ => order.Add(3);
            _profile.bindings = new[] { Binding(enter), Binding(exit, PresentationEvent.StateExit, 1), Binding(transition, PresentationEvent.Transition, 1) };
            _driver.Bind(_source); _source.Change(1, 2);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void DomainUsesExistingBusAndExactKey()
        {
            var cue = Asset<RecordingCue>(); var bus = new UniversalFsmSignalBus();
            _profile.bindings = new[] { Binding(cue, PresentationEvent.DomainEvent) }; _driver.Bind(_source, bus);
            bus.Publish(UniversalFsmSignal.Domain("damage")); Assert.AreEqual(0, cue.calls);
            bus.Publish(UniversalFsmSignal.Domain("Damage", intPayload: 19)); Assert.AreEqual(1, cue.calls); Assert.AreEqual(19, cue.last.IntPayload);
            _driver.enabled = false; bus.Publish(UniversalFsmSignal.Domain("Damage")); Assert.AreEqual(1, cue.calls);
        }

        [Test]
        public void DisableAndRepeatedResetCleanCustomCueOnce()
        {
            var cue = Asset<RecordingCue>(); _profile.bindings = new[] { Binding(cue) }; _driver.Bind(_source);
            _source.Change(0, 2); _driver.enabled = false; _driver.ResetPresentation();
            _source.Change(0, 2); Assert.AreEqual(1, cue.calls); Assert.AreEqual(1, cue.stops);
        }

        [Test]
        public void DisableInsideCueStopsRemainingActions()
        {
            var first = Asset<RecordingCue>(); first.onExecute = d => d.enabled = false;
            var second = Asset<RecordingCue>(); var binding = Binding(first); binding.actions = new PresentationCue[] { first, second };
            _profile.bindings = new[] { binding }; _driver.Bind(_source); _source.Change(0, 2);
            Assert.AreEqual(0, second.calls); Assert.AreEqual(1, first.stops);
        }

        [Test]
        public void RebindingDetachesOldSource()
        {
            var cue = Asset<RecordingCue>(); _profile.bindings = new[] { Binding(cue) }; _driver.Bind(_source);
            var next = new Source(); _driver.Bind(next); _source.Change(0, 2); next.Change(0, 2); Assert.AreEqual(1, cue.calls);
        }

        [Test]
        public void MirrorBindingConsumesCanonicalIdWithoutRevalidation()
        {
            var definition = Asset<UniversalFsmDefinitionAsset>();
            definition.SetDefinition(new UniversalFsmDefinition { states = new[] { new UniversalStateDef { id = 2 }, new UniversalStateDef { id = 3, mirror_of_id = 2 } } });
            _profile.definition = definition;
            var cue = Asset<RecordingCue>(); _profile.bindings = new[] { Binding(cue) }; _driver.Bind(_source);
            _source.Change(0, 3); Assert.AreEqual(1, cue.calls); Assert.AreEqual(2, cue.last.StateId);
        }

        [Test]
        public void RealUniversalFsmInitialSnapshotTransitionAndReset()
        {
            var definition = new UniversalFsmDefinition
            {
                start_state_id = 1, fallback_state_id = 1,
                states = new[] { new UniversalStateDef { id = 1, layer_id = "Main" }, new UniversalStateDef { id = 2, layer_id = "Main" } },
                layers = new[] { new UniversalLayerDef { id = "Main", start_state_id = 1, fallback_state_id = 1 } }
            };
            using var runtime = UniversalFsmRuntimeBuilder.Build(definition, new object(),
                new Dictionary<int, IUniversalFsmState<object>> { [1] = new State(1), [2] = new State(2) }, new UniversalConditionRegistry<object>());
            var first = Asset<RecordingCue>(); var second = Asset<RecordingCue>();
            var a = Binding(first, state: 1); var b = Binding(second); a.layerId = b.layerId = UniversalFsmBlackboard.StableHash("Main");
            _profile.bindings = new[] { a, b }; runtime.Initialize(); _driver.Bind((IUniversalFsmStateChangeSource)runtime);
            Assert.AreEqual(1, first.calls);
            Assert.IsTrue(runtime.TryForceTransition("Main", 2)); Assert.AreEqual(1, second.calls);
            _driver.ResetPresentation(); runtime.Reset(); Assert.AreEqual(2, first.calls); Assert.AreEqual(1, second.stops);
        }

        [UnityTest]
        public IEnumerator NativeAudioOneShotsUseOnlyAuthoredSourceAndStopOnDisable()
        {
            _driver.gameObject.AddComponent<AudioListener>();
            _driver.audioSource = _driver.gameObject.AddComponent<AudioSource>();
            var clip = AudioClip.Create("test tone", 44100, 1, 44100, false); _objects.Add(clip);
            var samples = new float[44100]; for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * 0.1f) * 0.1f; clip.SetData(samples, 0);
            var cue = Asset<AudioCue>(); cue.clip = clip; cue.volume = 0.4f;
            _profile.bindings = new[] { Binding(cue) }; _driver.Bind(_source); _source.Change(1, 2); _source.Change(1, 2);
            yield return null;
            Assert.IsTrue(_driver.audioSource.isPlaying); Assert.AreEqual(1, _driver.GetComponentsInChildren<AudioSource>(true).Length);
            Assert.AreEqual(0, _driver.transform.childCount); Assert.AreEqual(1, _driver.audioSource.pitch);
            _driver.enabled = false; Assert.IsFalse(_driver.audioSource.isPlaying);
        }

        [TestCase(-0.1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidAudioVolumeFailsClearly(float volume)
        {
            var cue = Asset<AudioCue>(); cue.volume = volume;
            Assert.Throws<InvalidOperationException>(() => cue.Execute(_driver, default));
        }

        private PresentationParticlePool Pool(float duration = 0.08f, float lifetime = 0.05f)
        {
            var template = Go("Particle template"); template.SetActive(false);
            var particle = template.AddComponent<ParticleSystem>(); var main = particle.main;
            main.loop = false; main.duration = duration; main.startLifetime = lifetime; main.playOnAwake = false;
            var emission = particle.emission; emission.rateOverTime = 30;
            var pool = Go("Particle pool").AddComponent<PresentationParticlePool>(); pool.Initialize(particle, 2);
            _driver.particlePools = new[] { pool }; return pool;
        }

        [UnityTest]
        public IEnumerator ParticleCallbackReturnsSlotsAndOverflowNeverInstantiates()
        {
            var pool = Pool(); int objects = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length;
            Assert.IsTrue(pool.TryPlay(_driver, Vector3.zero, Quaternion.identity));
            Assert.IsTrue(pool.TryPlay(_driver, Vector3.one, Quaternion.identity));
            Assert.IsFalse(pool.TryPlay(_driver, Vector3.zero, Quaternion.identity)); Assert.AreEqual(2, pool.ActiveCount);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.IsTrue(pool.TryPlay(_driver, Vector3.zero, Quaternion.identity));
            Assert.AreEqual(objects, Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length);
        }

        [Test]
        public void ParticleStateExitAndDisableReleaseOnlyOwnedSlots()
        {
            var pool = Pool(1, 1); _driver.Bind(_source);
            pool.TryPlay(_driver, Vector3.zero, Quaternion.identity, 7, 1, true);
            pool.TryPlay(_driver, Vector3.zero, Quaternion.identity, 7, 2, true);
            _source.Change(1, 2); Assert.AreEqual(1, pool.ActiveCount);
            _driver.enabled = false; Assert.AreEqual(0, pool.ActiveCount);
        }

        [UnityTest]
        public IEnumerator ClearThenImmediateReuseIsNotReleasedByStaleCallback()
        {
            var pool = Pool(1, 1); pool.TryPlay(_driver, Vector3.zero, Quaternion.identity);
            pool.Clear(_driver); pool.TryPlay(_driver, Vector3.zero, Quaternion.identity);
            yield return null; Assert.AreEqual(1, pool.ActiveCount);
        }

        [Test]
        public void ParticlePoolRejectsUnsupportedLoopingPrefab()
        {
            var template = Go("Loop template"); template.SetActive(false); var particle = template.AddComponent<ParticleSystem>();
            var pool = Go("Bad pool").AddComponent<PresentationParticlePool>();
            Assert.Throws<InvalidOperationException>(() => pool.Initialize(particle, 2));
        }

        private SpriteRenderer Sprite()
        {
            var texture = new Texture2D(8, 8); _objects.Add(texture);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f); _objects.Add(sprite);
            var renderer = _driver.gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = Color.green;
            return renderer;
        }

        [UnityTest]
        public IEnumerator AfterimageSnapshotsWorldPoseFadesAndReturns()
        {
            var source = Sprite(); source.sortingOrder = 3; source.flipX = true;
            var effect = _driver.gameObject.AddComponent<PresentationAfterimage>(); effect.source = source; _driver.afterimage = effect;
            _driver.transform.position = new Vector3(3, 2, 0);
            Assert.IsTrue(effect.Snapshot(new Color(1, 0, 0, 0.6f), 0.3f));
            var snapshots = Object.FindObjectsByType<SpriteRenderer>();
            SpriteRenderer? snapshot = null; foreach (var item in snapshots) if (item != source && item.enabled && item.sprite == source.sprite) snapshot = item;
            Assert.NotNull(snapshot); Assert.AreEqual(0.6f, snapshot!.color.a); Assert.AreEqual(3, snapshot.sortingOrder); Assert.IsTrue(snapshot.flipX);
            _driver.transform.position = Vector3.zero; Assert.AreEqual(new Vector3(3, 2, 0), snapshot.transform.position);
            yield return new WaitForSecondsRealtime(0.1f); Assert.Less(snapshot.color.a, 0.6f);
            yield return new WaitForSecondsRealtime(0.3f); Assert.AreEqual(0, effect.ActiveCount); Assert.IsFalse(effect.enabled);
            Assert.IsTrue(effect.Snapshot(Color.white, 1)); _driver.enabled = false; Assert.AreEqual(0, effect.ActiveCount);
        }

        [UnityTest]
        public IEnumerator SpriteFlashRestoresBaselineAcrossOverlap()
        {
            var source = Sprite(); var effect = _driver.gameObject.AddComponent<PresentationSpriteFlash>(); effect.renderers = new[] { source }; _driver.spriteFlash = effect;
            effect.Flash(Color.white, 0.1f); effect.Flash(Color.red, 0.1f); Assert.AreEqual(Color.red, source.color);
            yield return new WaitForSecondsRealtime(0.2f); Assert.AreEqual(Color.green, source.color); Assert.IsFalse(effect.enabled);
        }

        [Test]
        public void SpriteFlashDoesNotClobberExternalColor()
        {
            var source = Sprite(); var effect = _driver.gameObject.AddComponent<PresentationSpriteFlash>(); effect.renderers = new[] { source };
            effect.Flash(Color.white, 1); source.color = Color.blue; effect.Clear(); Assert.AreEqual(Color.blue, source.color);
        }

        [UnityTest]
        public IEnumerator HitStopOverlapExpiresIndependentlyAndRestores()
        {
            var effect = _driver.gameObject.AddComponent<PresentationHitStop>(); _driver.hitStop = effect; effect.scaleFixedDeltaTime = true;
            effect.RequestStop(_driver, 0.5f, 0.4f); effect.RequestStop(_driver, 0.1f, 0.1f);
            Assert.AreEqual(0.1f, Time.timeScale); Assert.Less(Time.fixedDeltaTime, _fixedDelta);
            yield return new WaitForSecondsRealtime(0.2f); Assert.AreEqual(0.5f, Time.timeScale);
            yield return new WaitForSecondsRealtime(0.3f); Assert.AreEqual(1, Time.timeScale); Assert.AreEqual(_fixedDelta, Time.fixedDeltaTime);
        }

        [Test]
        public void HitStopRespectsExternalPauseAndExplicitSameValuePause()
        {
            var effect = _driver.gameObject.AddComponent<PresentationHitStop>(); _driver.hitStop = effect;
            effect.RequestStop(_driver, 0.1f, 1); Time.timeScale = 0; effect.Cancel(_driver); Assert.AreEqual(0, Time.timeScale);
            Time.timeScale = 1; effect.RequestStop(_driver, 0, 1); effect.SetPaused(true); Time.timeScale = 0;
            effect.Cancel(_driver); Assert.AreEqual(0, Time.timeScale); Assert.AreEqual(0, effect.ActiveCount);
            effect.SetPaused(false); Time.timeScale = 1; effect.RequestStop(_driver, 0.2f, 1); _driver.enabled = false; Assert.AreEqual(1, Time.timeScale);
        }

        [Test]
        public void HitStopRefusesCompetingCoordinator()
        {
            var first = _driver.gameObject.AddComponent<PresentationHitStop>(); _driver.hitStop = first;
            var other = Go("Other coordinator").AddComponent<PresentationHitStop>();
            first.RequestStop(_driver, 0.2f, 1); Assert.Throws<InvalidOperationException>(() => other.RequestStop(_driver, 0.1f, 1));
        }

        [Test]
        public void WarmDispatchDoesNotAllocateManagedMemory()
        {
            var cue = Asset<RecordingCue>(); _profile.bindings = new[] { Binding(cue) }; _driver.Bind(_source);
            for (int i = 0; i < 32; i++) _source.Change(1, 2);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) _source.Change(1, 2);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "Presentation-only dispatch should not allocate after setup.");
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        private static IList Requests(PresentationHitStop effect) =>
            (IList)typeof(PresentationHitStop).GetField("_requests", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(effect)!;

        private static float Deadline(object request) => (float)request.GetType().GetField("End")!.GetValue(request)!;

        private static void Elapse(PresentationHitStop effect, float seconds)
        {
            // Advance only request deadlines, then exercise the real coordinator expiry code synchronously.
            // This avoids frame-rate-dependent assertions for the required .04/.08 intervals.
            var requests = Requests(effect);
            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i]!;
                request.GetType().GetField("End")!.SetValue(request, Deadline(request) - seconds);
                requests[i] = request;
            }
            effect.SendMessage("Update");
        }

        [Test]
        public void AuthoredHitStopRemainsDefaultAndIgnoresPayload()
        {
            var effect = _driver.gameObject.AddComponent<PresentationHitStop>(); _driver.hitStop = effect;
            var cue = Asset<HitStopCue>(); cue.scale = .08f; cue.duration = .12f;
            Assert.AreEqual(HitStopDurationSource.Authored, cue.durationSource);
            cue.Execute(_driver, UniversalFsmSignal.Domain("hit", domainPayload: new UniversalFsmDomainPayload(duration: .04f)));
            Assert.AreEqual(Time.unscaledTime + .12f, Deadline(Requests(effect)[0]!), .000001f);
            Assert.AreEqual(.08f, Time.timeScale);
            Elapse(effect, .05f); Assert.AreEqual(1, effect.ActiveCount);
            Elapse(effect, .08f); Assert.AreEqual(0, effect.ActiveCount); Assert.AreEqual(1, Time.timeScale);
        }

        [Test]
        public void SamePayloadCuePreservesDistinctDurationsAndIndependentExpiry()
        {
            var effect = _driver.gameObject.AddComponent<PresentationHitStop>(); _driver.hitStop = effect;
            var cue = Asset<HitStopCue>(); cue.scale = .08f; cue.durationSource = HitStopDurationSource.SignalPayload;
            var bus = new UniversalFsmSignalBus(); _profile.bindings = new[] { Binding(cue, PresentationEvent.DomainEvent) }; _driver.Bind(_source, bus);
            bus.Publish(UniversalFsmSignal.Domain("Damage", domainPayload: new UniversalFsmDomainPayload(duration: .04f)));
            bus.Publish(UniversalFsmSignal.Domain("Damage", domainPayload: new UniversalFsmDomainPayload(duration: .08f)));
            Assert.AreEqual(2, effect.ActiveCount);
            Assert.AreEqual(Time.unscaledTime + .04f, Deadline(Requests(effect)[0]!), .000001f);
            Assert.AreEqual(Time.unscaledTime + .08f, Deadline(Requests(effect)[1]!), .000001f);
            Elapse(effect, .05f); Assert.AreEqual(1, effect.ActiveCount); Assert.AreEqual(.08f, Time.timeScale);
            Elapse(effect, .04f); Assert.AreEqual(0, effect.ActiveCount); Assert.AreEqual(1, Time.timeScale);
        }

        [TestCase(.04f, 1, 0, .2f, .04f)]
        [TestCase(.4f, 1, 0, .2f, .2f)]
        [TestCase(.01f, 2, .03f, .2f, .03f)]
        [TestCase(.02f, 2, 0, .2f, .04f)]
        [TestCase(.03f, 1, .03f, .2f, .03f)]
        [TestCase(.2f, 1, .03f, .2f, .2f)]
        [TestCase(0, 1, .03f, .2f, .03f)]
        [TestCase(float.MaxValue, float.MaxValue, 0, .2f, .2f)]
        public void PayloadTransformPreservesClamps(float payload, float multiplier, float min, float max, float expected)
        {
            var effect = _driver.gameObject.AddComponent<PresentationHitStop>(); _driver.hitStop = effect;
            var cue = Asset<HitStopCue>(); cue.durationSource = HitStopDurationSource.SignalPayload;
            cue.payloadDurationMultiplier = multiplier; cue.minimumPayloadDuration = min; cue.maximumPayloadDuration = max;
            cue.Execute(_driver, UniversalFsmSignal.Domain("hit", domainPayload: new UniversalFsmDomainPayload(duration: payload)));
            Assert.AreEqual(Time.unscaledTime + expected, Deadline(Requests(effect)[0]!), .000001f);
        }

        [Test]
        public void MissingOrZeroPayloadDoesNotFallbackOrRequireCoordinator()
        {
            var cue = Asset<HitStopCue>(); cue.durationSource = HitStopDurationSource.SignalPayload; cue.duration = .5f;
            Assert.DoesNotThrow(() => cue.Execute(_driver, default)); Assert.AreEqual(1, Time.timeScale);
            var effect = _driver.gameObject.AddComponent<PresentationHitStop>(); _driver.hitStop = effect;
            cue.Execute(_driver, UniversalFsmSignal.Domain("hit")); Assert.AreEqual(0, effect.ActiveCount);
            cue.payloadDurationMultiplier = 0;
            cue.Execute(_driver, UniversalFsmSignal.Domain("hit", domainPayload: new UniversalFsmDomainPayload(duration: .1f)));
            Assert.AreEqual(0, effect.ActiveCount);
        }

        [TestCase("payload", float.NaN)] [TestCase("payload", float.PositiveInfinity)] [TestCase("payload", -1)]
        [TestCase("payloadDurationMultiplier", float.NaN)] [TestCase("payloadDurationMultiplier", float.PositiveInfinity)] [TestCase("payloadDurationMultiplier", -1)]
        [TestCase("minimumPayloadDuration", float.NaN)] [TestCase("minimumPayloadDuration", float.PositiveInfinity)] [TestCase("minimumPayloadDuration", -1)]
        [TestCase("maximumPayloadDuration", float.NaN)] [TestCase("maximumPayloadDuration", float.PositiveInfinity)] [TestCase("maximumPayloadDuration", -1)]
        [TestCase("scale", float.NaN)] [TestCase("scale", float.PositiveInfinity)] [TestCase("scale", -1)] [TestCase("scale", 2)]
        public void InvalidPayloadInputFailsBeforeRequest(string field, float value)
        {
            var effect = _driver.gameObject.AddComponent<PresentationHitStop>(); _driver.hitStop = effect;
            var cue = Asset<HitStopCue>(); cue.durationSource = HitStopDurationSource.SignalPayload;
            if (field != "payload") typeof(HitStopCue).GetField(field)!.SetValue(cue, value);
            var signal = UniversalFsmSignal.Domain("hit", domainPayload: new UniversalFsmDomainPayload(duration: field == "payload" ? value : .04f));
            Assert.Throws<InvalidOperationException>(() => cue.Execute(_driver, signal)); Assert.AreEqual(0, effect.ActiveCount);
        }

        [Test]
        public void ReversedClampsAndUnknownModeFailDeterministically()
        {
            var cue = Asset<HitStopCue>(); cue.durationSource = HitStopDurationSource.SignalPayload;
            cue.minimumPayloadDuration = .3f; cue.maximumPayloadDuration = .2f;
            Assert.Throws<InvalidOperationException>(() => cue.Execute(_driver, default));
            cue.durationSource = (HitStopDurationSource)99;
            Assert.Throws<InvalidOperationException>(() => cue.Execute(_driver, default));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void AuthoredDurationStillRequiresPositiveFiniteValue(float duration)
        {
            _driver.hitStop = _driver.gameObject.AddComponent<PresentationHitStop>();
            var cue = Asset<HitStopCue>(); cue.duration = duration;
            Assert.Throws<ArgumentOutOfRangeException>(() => cue.Execute(_driver, default));
        }

        [Test]
        public void FullCustomPayloadSurvivesForgedDomainStateMetadata()
        {
            var cue = Asset<RecordingCue>(); var other = Asset<RecordingCue>(); var bus = new UniversalFsmSignalBus();
            var binding = Binding(cue, PresentationEvent.DomainEvent);
            _profile.bindings = new[] { binding, Binding(other) }; _driver.Bind(_source, bus);
            var payload = new UniversalFsmDomainPayload("primary", "secondary", 9876543210, 12, 34, 56, 7.5f, .04f, new Vector2(3, 4), new Vector2(-1, 2));
            var signal = UniversalFsmSignal.Domain("Damage", 999, "forged", 321, "forged-state", 17, payload);
            bus.Publish(signal);
            Assert.AreEqual(1, cue.calls); Assert.AreEqual(0, other.calls);
            Assert.AreEqual(signal, cue.last); Assert.AreEqual(payload, cue.last.DomainPayload);
            var actual = cue.last.DomainPayload;
            Assert.AreEqual("primary", actual.PrimaryId); Assert.AreEqual("secondary", actual.SecondaryId);
            Assert.AreEqual(9876543210, actual.Identity); Assert.AreEqual(12, actual.SourceId); Assert.AreEqual(34, actual.TargetId);
            Assert.AreEqual(56, actual.Flags); Assert.AreEqual(7.5f, actual.Value); Assert.AreEqual(.04f, actual.Duration);
            Assert.AreEqual(new Vector2(3, 4), actual.Position); Assert.AreEqual(new Vector2(-1, 2), actual.Direction);
            Assert.AreEqual(17, cue.last.IntPayload); Assert.AreEqual("Damage", cue.last.EventKey);
            bus.Publish(UniversalFsmSignal.Domain("wrong", 7, "Main", 2)); Assert.AreEqual(1, cue.calls);
        }

        [Test]
        public void AfterimageFirstObservableFrameKeepsExactAuthoredHalfAlpha()
        {
            var source = Sprite(); var effect = _driver.gameObject.AddComponent<PresentationAfterimage>(); effect.source = source;
            _driver.afterimage = effect; var tint = new Color(.2f, .3f, .4f, .5f);
            Assert.IsTrue(effect.Snapshot(tint, .16f));
            effect.SendMessage("Update"); // Same frame, including an Update after the snapshot, must not consume prior frame delta.
            SpriteRenderer? snapshot = null;
            foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>())
                if (renderer != source && renderer.enabled && renderer.sprite == source.sprite) snapshot = renderer;
            Assert.NotNull(snapshot); Assert.AreEqual(tint, snapshot!.color);
        }

        [Test]
        public void ControllerBindingUsesOwnedRuntimeDefinitionAfterAuthoringReferenceChanges()
        {
            var actor = Go("Definition provenance"); actor.SetActive(false);
            var controller = actor.AddComponent<UniversalFsmRuntimeController>();
            var definition = Asset<UniversalFsmDefinitionAsset>();
            definition.SetDefinition(new UniversalFsmDefinition
            {
                domain_id = "PresentationRegression", context_type_name = typeof(RegressionContext).AssemblyQualifiedName,
                start_state_id = 1, fallback_state_id = 1,
                states = new[] { new UniversalStateDef { id = 1, name = "Idle", layer_id = "Main", state_type_name = typeof(RegressionIdleState).AssemblyQualifiedName } },
                layers = new[] { new UniversalLayerDef { id = "Main", start_state_id = 1, fallback_state_id = 1 } }
            });
            SetField(controller, "_fsmAsset", definition); SetField(controller, "_profile", Asset<RegressionDomainProfile>());
            controller.SetTickMode(false, false); actor.SetActive(true);
            Assert.NotNull(controller.Runtime); Assert.IsTrue(controller.Runtime!.IsInitialized);
            Assert.AreSame(definition, controller.RuntimeDefinitionAsset);
            var unrelated = Asset<UniversalFsmDefinitionAsset>(); SetField(controller, "_fsmAsset", unrelated);
            Assert.AreSame(unrelated, controller.DefinitionAsset); Assert.AreSame(definition, controller.RuntimeDefinitionAsset);
            _driver.controller = controller; _profile.definition = definition;
            var bind = typeof(FsmPresentationDriver).GetMethod("BindController", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.DoesNotThrow(() => bind.Invoke(_driver, null));
            _profile.definition = unrelated;
            var error = Assert.Throws<TargetInvocationException>(() => bind.Invoke(_driver, null));
            Assert.IsInstanceOf<InvalidOperationException>(error!.InnerException);
            _driver.Unbind(); _driver.controller = null;
        }
    }
}
