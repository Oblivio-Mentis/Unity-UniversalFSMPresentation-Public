#nullable enable
using System;
using System.IO;
using OblivioMentis.UniversalFSM;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OblivioMentis.Presentation.Samples.Editor
{
    public static class BasicPresentationSceneBuilder
    {
        [MenuItem("Tools/Примеры Presentation/Создать демонстрационную сцену")]
        public static void Create() => CreateScene();

        public static string CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return string.Empty;
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/PresentationValidation");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.gameObject.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 4;
            camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();
            var cameraImpulse = camera.gameObject.AddComponent<BasicCameraImpulse>();
            var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional;
            var actor = new GameObject("Presentation sample actor"); actor.SetActive(false);
            var sprite = actor.AddComponent<SpriteRenderer>(); sprite.color = new Color(0.1f, 0.8f, 0.9f);
            var texture = new Texture2D(32, 32); var pixels = new Color[1024]; Array.Fill(pixels, Color.white); texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(folder + "/Square.png", texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(folder + "/Square.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(folder + "/Square.png"); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 32; importer.SaveAndReimport();
            sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/Square.png");
            if (sprite.sprite == null) throw new InvalidOperationException("Созданное изображение квадрата не импортировано как одиночный Sprite.");

            var animator = actor.AddComponent<Animator>();
            var animation = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Actor.controller");
            var idle = animation.layers[0].stateMachine.AddState("Idle"); var run = animation.layers[0].stateMachine.AddState("Run"); animation.layers[0].stateMachine.defaultState = idle;
            var idleClip = new AnimationClip(); idleClip.SetCurve("", typeof(Transform), "m_LocalScale.y", AnimationCurve.Constant(0, 1, 1)); AssetDatabase.CreateAsset(idleClip, folder + "/Idle.anim"); idle.motion = idleClip;
            var runClip = new AnimationClip(); runClip.SetCurve("", typeof(Transform), "m_LocalScale.y", new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.2f, 1.6f), new Keyframe(0.4f, 1)));
            var settings = AnimationUtility.GetAnimationClipSettings(runClip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(runClip, settings); AssetDatabase.CreateAsset(runClip, folder + "/Run.anim"); run.motion = runClip;
            animator.runtimeAnimatorController = animation;
            AddClip(animation, folder, "Jump", "m_LocalPosition.y", new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.25f, 1.5f), new Keyframe(0.5f, 0)));
            var attack = AddClip(animation, folder, "Attack", "m_LocalScale.x", new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.15f, 1.6f), new Keyframe(0.3f, 1)));
            animation.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var attackTransition = animation.layers[0].stateMachine.AddAnyStateTransition(attack);
            attackTransition.hasExitTime = false;
            attackTransition.hasFixedDuration = true;
            attackTransition.duration = 0.05f;
            attackTransition.canTransitionToSelf = false;
            attackTransition.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            var attachment = new GameObject("Weapon attachment"); attachment.transform.SetParent(actor.transform, false);
            attachment.transform.localPosition = new Vector3(0.7f, 0, 0); attachment.transform.localScale = new Vector3(0.5f, 0.15f, 1);
            var attachmentRenderer = attachment.AddComponent<SpriteRenderer>(); attachmentRenderer.sprite = sprite.sprite; attachmentRenderer.color = new Color(1, 0.65f, 0.2f); attachmentRenderer.enabled = false;
            var anchors = actor.AddComponent<PresentationAnchorRegistry>();
            anchors.entries = new[] { new PresentationAnchorRegistry.Entry { key = "weapon", target = attachment.transform, renderers = new Renderer[] { attachmentRenderer } } };

            WriteTone(folder + "/Tone.wav"); AssetDatabase.ImportAsset(folder + "/Tone.wav");
            var audio = actor.AddComponent<AudioSource>(); audio.playOnAwake = false;
            var particleObject = new GameObject("Particle template"); particleObject.SetActive(false);
            var particle = particleObject.AddComponent<ParticleSystem>(); var main = particle.main;
            main.loop = false; main.duration = 0.4f; main.startLifetime = 0.5f; main.startSpeed = 2; main.startSize = 0.12f; main.playOnAwake = false;
            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            var shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Не найден подходящий шейдер частиц.");
            var material = new Material(shader); AssetDatabase.CreateAsset(material, folder + "/Particles.mat"); renderer.sharedMaterial = material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(particleObject, folder + "/Particles.prefab").GetComponent<ParticleSystem>(); UnityEngine.Object.DestroyImmediate(particleObject);
            var pool = actor.AddComponent<PresentationParticlePool>(); var serializedPool = new SerializedObject(pool);
            serializedPool.FindProperty("_prefab").objectReferenceValue = prefab; serializedPool.FindProperty("_capacity").intValue = 4; serializedPool.ApplyModifiedPropertiesWithoutUndo();
            var afterimage = actor.AddComponent<PresentationAfterimage>(); afterimage.source = sprite;
            var flash = actor.AddComponent<PresentationSpriteFlash>(); flash.renderers = new[] { sprite };
            var hitStop = new GameObject("Shared hit-stop coordinator").AddComponent<PresentationHitStop>();

            var definition = ScriptableObject.CreateInstance<UniversalFsmDefinitionAsset>();
            definition.SetDefinition(new UniversalFsmDefinition
            {
                domain_id = "PresentationSample", context_type_name = typeof(BasicPresentationContext).AssemblyQualifiedName!, start_state_id = 1, fallback_state_id = 1,
                layers = new[] { new UniversalLayerDef { id = "Main", name = "Основной", start_state_id = 1, fallback_state_id = 1 } },
                states = new[] {
                    new UniversalStateDef { id = 1, name = "Покой", layer_id = "Main", state_type_name = typeof(BasicIdleState).AssemblyQualifiedName! },
                    new UniversalStateDef { id = 2, name = "Бег", layer_id = "Main", state_type_name = typeof(BasicRunState).AssemblyQualifiedName! },
                    new UniversalStateDef { id = 3, name = "Прыжок", layer_id = "Main", state_type_name = typeof(BasicJumpState).AssemblyQualifiedName! },
                    new UniversalStateDef { id = 4, name = "Атака", layer_id = "Main", state_type_name = typeof(BasicAttackState).AssemblyQualifiedName! }
                }
            });
            AssetDatabase.CreateAsset(definition, folder + "/FSM.asset");
            var domain = ScriptableObject.CreateInstance<BasicPresentationDomainProfile>(); AssetDatabase.CreateAsset(domain, folder + "/Domain.asset");
            var controller = actor.AddComponent<UniversalFsmRuntimeController>(); var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("_fsmAsset").objectReferenceValue = definition; serializedController.FindProperty("_profile").objectReferenceValue = domain; serializedController.ApplyModifiedPropertiesWithoutUndo();

            var idleCue = Make<AnimatorStateCue>(folder, "IdleCue"); idleCue.state = "Base Layer.Idle";
            var runCue = Make<AnimatorStateCue>(folder, "RunCue"); runCue.state = "Base Layer.Run";
            var jumpCue = Make<AnimatorStateCue>(folder, "JumpCue"); jumpCue.state = "Base Layer.Jump";
            var triggerCue = Make<AnimatorTriggerCue>(folder, "AttackTriggerCue"); triggerCue.parameter = "Attack";
            var resetTrigger = Make<AnimatorTriggerCue>(folder, "ResetAttackTrigger"); resetTrigger.parameter = "Attack"; resetTrigger.reset = true;
            var showWeapon = Make<AttachmentCue>(folder, "ShowWeapon"); showWeapon.anchor = "weapon";
            var hideWeapon = Make<AttachmentCue>(folder, "HideWeapon"); hideWeapon.anchor = "weapon"; hideWeapon.visible = false;
            var cameraCue = Make<CameraImpulseCue>(folder, "CameraCue"); cameraCue.amplitude = 0.12f; cameraCue.duration = 0.15f;
            var customCue = Make<SampleTintCue>(folder, "CustomTintCue");
            var audioCue = Make<AudioCue>(folder, "AudioCue"); audioCue.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "/Tone.wav"); audioCue.volume = 0.3f;
            var particleCue = Make<ParticleCue>(folder, "ParticleCue"); particleCue.prefab = prefab; particleCue.stopOnStateExit = true;
            var flashCue = Make<SpriteFlashCue>(folder, "FlashCue"); flashCue.color = Color.white; flashCue.duration = 0.3f;
            var afterCue = Make<AfterimageCue>(folder, "AfterimageCue"); afterCue.duration = 0.8f;
            var hitCue = Make<HitStopCue>(folder, "HitStopCue"); hitCue.duration = 0.12f; hitCue.scale = 0.05f;
            hitCue.durationSource = HitStopDurationSource.SignalPayload;
            var profile = Make<FsmPresentationProfile>(folder, "Presentation"); profile.definition = definition;
            int layer = UniversalFsmBlackboard.StableHash("Main");
            profile.bindings = new[] {
                new PresentationBinding { layerId = layer, stateId = 1, actions = new PresentationCue[] { idleCue, hideWeapon } },
                new PresentationBinding { layerId = layer, stateId = 2, actions = new PresentationCue[] { runCue, audioCue, particleCue } },
                new PresentationBinding { layerId = layer, stateId = 3, actions = new PresentationCue[] { jumpCue, afterCue } },
                new PresentationBinding { layerId = layer, stateId = 4, actions = new PresentationCue[] { triggerCue, showWeapon, cameraCue, customCue } },
                new PresentationBinding { trigger = PresentationEvent.StateExit, layerId = layer, stateId = 4, actions = new PresentationCue[] { hideWeapon, resetTrigger } },
                new PresentationBinding { trigger = PresentationEvent.DomainEvent, eventKey = "Damage", actions = new PresentationCue[] { flashCue, afterCue, hitCue } }
            };
            var driver = actor.AddComponent<FsmPresentationDriver>(); driver.controller = controller; driver.profile = profile; driver.animator = animator; driver.audioSource = audio;
            driver.particlePools = new[] { pool }; driver.afterimage = afterimage; driver.spriteFlash = flash; driver.hitStop = hitStop;
            driver.anchors = anchors; driver.cameraSink = cameraImpulse;
            var demo = actor.AddComponent<BasicPresentationDemo>(); demo.driver = driver; demo.controller = controller;
            foreach (var asset in new UnityEngine.Object[] { idleCue, runCue, jumpCue, triggerCue, resetTrigger, showWeapon, hideWeapon, cameraCue, customCue, audioCue, particleCue, flashCue, afterCue, hitCue, profile, animation }) EditorUtility.SetDirty(asset);
            actor.SetActive(true); AssetDatabase.SaveAssets();
            string scenePath = folder + "/PresentationValidation.unity"; EditorSceneManager.SaveScene(scene, scenePath); return scenePath;
        }

        private static T Make<T>(string folder, string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, folder + "/" + name + ".asset"); return asset;
        }

        private static AnimatorState AddClip(AnimatorController controller, string folder, string name, string property, AnimationCurve curve)
        {
            var clip = new AnimationClip(); clip.SetCurve("", typeof(Transform), property, curve);
            AssetDatabase.CreateAsset(clip, folder + "/" + name + ".anim");
            var state = controller.layers[0].stateMachine.AddState(name);
            state.motion = clip;
            return state;
        }

        private static void WriteTone(string path)
        {
            const int count = 11025;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (int i = 0; i < count; i++) writer.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / 44100) * 8000 * (1f - (float)i / count)));
        }
    }
}
