using System;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;

namespace UnityEditor.Perception.GroundTruth
{
    [CustomEditor(typeof(PerceptionCamera))]
    public class PerceptionCameraEditor : Editor
    {
        ReorderableList m_LabelersList;

        public void OnEnable()
        {
            m_LabelersList = new ReorderableList(this.serializedObject, this.serializedObject.FindProperty(nameof(PerceptionCamera.labelers)), true, false, true, true);
            m_LabelersList.elementHeightCallback = GetElementHeight;
            m_LabelersList.drawElementCallback = DrawElement;
            m_LabelersList.onAddCallback += OnAdd;
            m_LabelersList.onRemoveCallback += OnRemove;
        }

        float GetElementHeight(int index)
        {
            var serializedProperty = this.serializedObject.FindProperty(nameof(PerceptionCamera.labelers));
            var element = serializedProperty.GetArrayElementAtIndex(index);
            var editor = Editor.CreateEditor(element.managedReferenceValue);
            return 10;
        }

        void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            var element = this.serializedObject.FindProperty(nameof(PerceptionCamera.labelers)).GetArrayElementAtIndex(index);
            var editor = Editor.CreateEditor(element.managedReferenceValue);
            editor.OnInspectorGUI();
        }

        void OnRemove(ReorderableList list)
        {
            throw new System.NotImplementedException();
        }

        void OnAdd(ReorderableList list)
        {
            var labelers = this.serializedObject.FindProperty(nameof(PerceptionCamera.labelers));
            labelers.InsertArrayElementAtIndex(0);
            var element = labelers.GetArrayElementAtIndex(0);
            element.managedReferenceValue = new BoundingBoxLabeler();
            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.description)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.period)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.startTime)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.captureRgbImages)));
            //EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.labelers)));
            m_LabelersList.DoLayoutList();
        }
    }
}
