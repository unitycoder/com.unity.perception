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
            // m_LabelersList = new ReorderableList(this.serializedObject, this.serializedObject.FindProperty(nameof(PerceptionCamera.labelers)), true, false, true, true);
            // m_LabelersList.elementHeightCallback = GetElementHeight;
            // m_LabelersList.drawElementCallback = DrawElement;
            // m_LabelersList.onAddCallback += OnAdd;
            // m_LabelersList.onRemoveCallback += OnRemove;
        }

        float GetElementHeight(int index)
        {
            throw new System.NotImplementedException();
        }

        void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            throw new NotImplementedException();
        }

        void OnRemove(ReorderableList list)
        {
            throw new System.NotImplementedException();
        }

        void OnAdd(ReorderableList list)
        {
            throw new System.NotImplementedException();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.description)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.period)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.startTime)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.captureRgbImages)));
            //m_LabelersList.DoLayoutList();
        }
    }
}
