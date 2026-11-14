#nullable enable
using System.Collections.Generic;
using OblivioMentis.UniversalFSM;
using OblivioMentis.UniversalFSM.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OblivioMentis.Presentation.Editor
{
    internal sealed class UniversalFsmPresentationWorkspaceProvider : IUniversalFsmEditorWorkspaceProvider
    {
        public UniversalFsmPresentationWorkspaceProvider() { }
        public string Id => "presentation";
        public string DisplayName => "Presentation";
        public int Order => 20;
        public bool Supports(UniversalFsmDefinitionAsset? asset) => asset != null;
        public IUniversalFsmEditorWorkspace CreateWorkspace(UniversalFsmEditorWorkspaceContext context) => new Workspace();

        private sealed class Workspace : IUniversalFsmEditorWorkspace
        {
            private readonly ObjectField _profile = new("Profile") { objectType = typeof(FsmPresentationProfile), allowSceneObjects = false };
            private UniversalFsmDefinitionAsset? _definition;
            public VisualElement Sidebar { get; } = new VisualElement();
            public VisualElement MainContent { get; } = new ScrollView();
            public IReadOnlyDictionary<string, int> StateBadgeCounts { get; } = new Dictionary<string, int>();

            public Workspace()
            {
                Sidebar.Add(new Label("Presentation mappings"));
                Sidebar.Add(_profile);
                Sidebar.Add(new Button(CreateProfile) { text = "Create profile" });
                _profile.RegisterValueChangedCallback(_ => Refresh());
            }

            public void SetAsset(UniversalFsmDefinitionAsset? asset)
            {
                _definition = asset;
                _profile.SetValueWithoutNotify(null);
                Refresh();
            }

            public void Refresh()
            {
                MainContent.Clear();
                if (_profile.value is FsmPresentationProfile profile)
                {
                    if (profile.definition != _definition)
                        MainContent.Add(new HelpBox("This profile references another FSM definition.", HelpBoxMessageType.Warning));
                    MainContent.Add(new InspectorElement(profile));
                }
                else MainContent.Add(new Label("Select or create a profile, select an FSM state in its Inspector, and add enter/exit actions."));
            }

            private void CreateProfile()
            {
                string path = EditorUtility.SaveFilePanelInProject("Create Presentation profile", "Presentation", "asset", "Choose profile location");
                if (string.IsNullOrEmpty(path)) return;
                var profile = ScriptableObject.CreateInstance<FsmPresentationProfile>();
                profile.definition = _definition;
                AssetDatabase.CreateAsset(profile, path);
                Undo.RegisterCreatedObjectUndo(profile, "Create Presentation profile");
                _profile.value = profile;
            }

            public void Dispose() { MainContent.Clear(); Sidebar.Clear(); }
        }
    }
}
