#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using OblivioMentis.UniversalFSM;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OblivioMentis.Presentation.Editor
{
    [CustomEditor(typeof(FsmPresentationProfile))]
    internal sealed class FsmPresentationProfileEditor : UnityEditor.Editor
    {
        private string _search = "", _layer = "";
        private int _stateId = -1;
        private bool _advanced, _transitions, _events;
        private readonly Dictionary<int, ReorderableList> _lists = new();
        private FsmPresentationProfile Profile => (FsmPresentationProfile)target;
        private UniversalStateDef[] States => Profile.definition == null ? Array.Empty<UniversalStateDef>() : Profile.definition.Definition.states;

        private void OnEnable() => Undo.undoRedoPerformed += RefreshLists;
        private void OnDisable() => Undo.undoRedoPerformed -= RefreshLists;
        private void RefreshLists() { _lists.Clear(); Repaint(); }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("definition"));
            if (serializedObject.ApplyModifiedProperties()) _lists.Clear();
            if (States.Length == 0) EditorGUILayout.HelpBox("Assign an FSM definition with states to author enter, exit and transition effects.", MessageType.Info);
            else DrawState();
            _transitions = EditorGUILayout.Foldout(_transitions, "TRANSITIONS", true);
            if (_transitions) DrawEntries(PresentationEvent.Transition);
            _events = EditorGUILayout.Foldout(_events, "DOMAIN EVENTS", true);
            if (_events) DrawEntries(PresentationEvent.DomainEvent);
            _advanced = EditorGUILayout.Foldout(_advanced, "Advanced / Raw Bindings", true);
            if (_advanced)
            {
                EditorGUILayout.HelpBox("Exact numeric IDs are owned by UniversalFSM. Raw edits affect the same bindings shown above.", MessageType.Info);
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bindings"), true);
                if (serializedObject.ApplyModifiedProperties()) _lists.Clear();
            }
        }
        private void DrawState()
        {
            _search = EditorGUILayout.TextField("Search states", _search);
            string[] layers = States.Select(PresentationAuthoring.Layer).Distinct().ToArray();
            _layer = layers[EditorGUILayout.Popup("Layer", Math.Max(0, Array.IndexOf(layers, _layer)), layers)];
            var states = States.Where(s => PresentationAuthoring.Layer(s) == _layer && s.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            if (states.Length == 0) { EditorGUILayout.HelpBox("No states match this search.", MessageType.Info); return; }
            int selected = Math.Max(0, Array.FindIndex(states, s => s.id == _stateId));
            selected = EditorGUILayout.Popup("State", selected, states.Select(StateLabel).ToArray());
            var state = states[selected];
            _stateId = state.id;
            EditorGUILayout.LabelField(state.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Layer", _layer);
            if (state.mirror_of_id >= 0) EditorGUILayout.HelpBox("Mirror state: effects use its canonical state in this layer.", MessageType.None);
            if (_advanced)
            {
                EditorGUILayout.LabelField("Canonical state ID", PresentationAuthoring.Canonical(state).ToString());
                EditorGUILayout.LabelField("Layer ID", PresentationAuthoring.LayerId(state).ToString());
            }
            DrawStateSection(state, PresentationEvent.StateEnter, "ON ENTER", "Enter");
            DrawStateSection(state, PresentationEvent.StateExit, "ON EXIT", "Exit");
        }
        private static string StateLabel(UniversalStateDef state) => state.name + (state.mirror_of_id >= 0 ? " (mirror)" : "");
        private void DrawStateSection(UniversalStateDef state, PresentationEvent trigger, string title, string suffix)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var key = PresentationAuthoring.StateBinding(state, trigger);
            bool found = false;
            for (int i = 0; i < Profile.bindings.Length; i++)
                if (Profile.bindings[i] != null && PresentationAuthoring.Matches(Profile.bindings[i], key))
                {
                    if (found) EditorGUILayout.HelpBox("Additional exact binding; executes after the preceding binding.", MessageType.Info);
                    DrawActions(i, key, state.name + " " + suffix);
                    found = true;
                }
            if (!found) DrawAdd(-1, key, state.name + " " + suffix);
        }
        private void DrawEntries(PresentationEvent trigger)
        {
            for (int i = 0; i < Profile.bindings.Length; i++)
            {
                var binding = Profile.bindings[i];
                if (binding == null || binding.trigger != trigger) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    serializedObject.Update();
                    var entry = serializedObject.FindProperty("bindings").GetArrayElementAtIndex(i);
                    if (trigger == PresentationEvent.DomainEvent)
                    {
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("eventKey"), new GUIContent("Event Key"));
                        if (string.IsNullOrWhiteSpace(binding.eventKey)) EditorGUILayout.HelpBox("Enter an exact nonempty event key.", MessageType.Warning);
                    }
                    else DrawTransition(entry, binding);
                    serializedObject.ApplyModifiedProperties();
                    DrawActions(i, binding, trigger == PresentationEvent.DomainEvent ? binding.eventKey : "Transition");
                    if (GUILayout.Button("Remove binding (keep assets)"))
                    {
                        serializedObject.Update();
                        serializedObject.FindProperty("bindings").DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        _lists.Clear();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            using (new EditorGUI.DisabledScope(trigger == PresentationEvent.Transition && States.Length == 0))
                if (GUILayout.Button(trigger == PresentationEvent.Transition ? "+ Add Transition" : "+ Add Domain Event"))
                {
                    PresentationAuthoring.AppendBinding(Profile, trigger == PresentationEvent.Transition
                        ? PresentationAuthoring.StateBinding(States[0], trigger, States[0])
                        : new PresentationBinding { trigger = trigger });
                    _lists.Clear();
                }
        }
        private void DrawTransition(SerializedProperty entry, PresentationBinding binding)
        {
            var from = PickState("From state", States, binding.layerId, binding.stateId);
            if (from != null)
            {
                entry.FindPropertyRelative("layerId").intValue = PresentationAuthoring.LayerId(from);
                entry.FindPropertyRelative("stateId").intValue = PresentationAuthoring.Canonical(from);
            }
            int layer = entry.FindPropertyRelative("layerId").intValue;
            var sameLayer = States.Where(s => PresentationAuthoring.LayerId(s) == layer).ToArray();
            var to = PickState("To state", sameLayer, layer, binding.toStateId);
            if (to != null) entry.FindPropertyRelative("toStateId").intValue = PresentationAuthoring.Canonical(to);
        }
        private static UniversalStateDef? PickState(string label, UniversalStateDef[] states, int layer, int id)
        {
            int current = Array.FindIndex(states, s => PresentationAuthoring.LayerId(s) == layer && PresentationAuthoring.Canonical(s) == id);
            string[] labels = new[] { "Select state..." }.Concat(states.Select(s => PresentationAuthoring.Layer(s) + "/" + StateLabel(s))).ToArray();
            int choice = EditorGUILayout.Popup(label, current + 1, labels) - 1;
            if (current < 0) EditorGUILayout.HelpBox("Choose a state from the assigned definition.", MessageType.Warning);
            return choice >= 0 && choice != current ? states[choice] : null;
        }
        private void DrawActions(int index, PresentationBinding key, string label)
        {
            serializedObject.Update();
            if (!_lists.TryGetValue(index, out var list))
            {
                var actions = serializedObject.FindProperty("bindings").GetArrayElementAtIndex(index).FindPropertyRelative("actions");
                list = new ReorderableList(serializedObject, actions, true, true, false, false);
                list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Effects — drag handles to reorder");
                list.elementHeightCallback = row => CueHeight(list.serializedProperty.GetArrayElementAtIndex(row));
                list.drawElementCallback = (rect, row, active, focused) => DrawCue(rect, list.serializedProperty, row);
                _lists[index] = list;
            }
            list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
            DrawAdd(index, key, label);
        }
        private static float CueHeight(SerializedProperty reference)
        {
            float height = 26;
            if (reference.objectReferenceValue is not PresentationCue cue) return height;
            using var data = new SerializedObject(cue);
            var field = data.GetIterator();
            if (field.NextVisible(true)) while (field.NextVisible(false)) height += EditorGUI.GetPropertyHeight(field, true) + 2;
            return height + 5;
        }
        private static void DrawCue(Rect rect, SerializedProperty actions, int row)
        {
            var reference = actions.GetArrayElementAtIndex(row);
            rect.y += 2;
            var line = new Rect(rect.x, rect.y, rect.width - 58, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, reference, GUIContent.none);
            if (GUI.Button(new Rect(rect.xMax - 54, rect.y, 26, line.height), new GUIContent("↗", "Ping cue asset")))
                EditorGUIUtility.PingObject(reference.objectReferenceValue);
            if (GUI.Button(new Rect(rect.xMax - 26, rect.y, 26, line.height), new GUIContent("×", "Remove reference; keep asset")))
            {
                reference.objectReferenceValue = null;
                actions.DeleteArrayElementAtIndex(row);
                actions.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
            if (reference.objectReferenceValue is not PresentationCue cue) return;
            using var data = new SerializedObject(cue);
            data.Update();
            var field = data.GetIterator();
            line.y += 24;
            line.width = rect.width;
            if (field.NextVisible(true)) while (field.NextVisible(false))
            {
                line.height = EditorGUI.GetPropertyHeight(field, true);
                EditorGUI.PropertyField(line, field, true);
                line.y += line.height + 2;
            }
            data.ApplyModifiedProperties();
        }
        private void DrawAdd(int index, PresentationBinding key, string label)
        {
            if (!AssetDatabase.GetAssetPath(Profile).StartsWith("Assets/", StringComparison.Ordinal))
                EditorGUILayout.HelpBox("Save a copy of this profile under Assets to create sibling cue assets.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!AssetDatabase.GetAssetPath(Profile).StartsWith("Assets/", StringComparison.Ordinal)))
                if (GUILayout.Button("+ Add Effect"))
                {
                    var menu = new GenericMenu();
                    foreach (var effect in PresentationAuthoring.Effects)
                    {
                        var choice = effect;
                        menu.AddItem(new GUIContent(choice.menu), false, () =>
                        {
                            PresentationAuthoring.CreateEffect(Profile, index, key, choice.type, label + " - " + choice.menu.Split('/')[1]);
                            _lists.Clear();
                            Repaint();
                        });
                    }
                    menu.ShowAsContext();
                }
            var existing = (PresentationCue?)EditorGUILayout.ObjectField("Reuse existing cue", null, typeof(PresentationCue), false);
            if (existing != null)
            {
                PresentationAuthoring.AddReference(Profile, index, key, existing);
                _lists.Clear();
            }
        }
        internal static void AddBinding(FsmPresentationProfile profile, UniversalStateDef state, PresentationEvent trigger) =>
            PresentationAuthoring.AppendBinding(profile, PresentationAuthoring.StateBinding(state, trigger));
    }
}
