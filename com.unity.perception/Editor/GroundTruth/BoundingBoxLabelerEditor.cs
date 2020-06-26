using UnityEngine;
using UnityEngine.Perception.GroundTruth;

namespace UnityEditor.Perception.GroundTruth
{
    // [CustomEditor(typeof(BoundingBoxLabeler))]
    // public class BoundingBoxLabelerEditor : Editor
    // {
    //     public override void OnInspectorGUI()
    //     {
    //         // base.OnInspectorGUI();
    //         // return;
    //         EditorGUILayout.PropertyField(this.serializedObject.FindProperty(nameof(BoundingBoxLabeler.annotationId)));
    //         var serializedProperty = this.serializedObject.FindProperty(nameof(BoundingBoxLabeler.labelingConfiguration));
    //
    //         if (serializedProperty.objectReferenceValue != null)
    //         {
    //             EditorGUILayout.Separator();
    //             var editor = Editor.CreateEditor(serializedProperty.objectReferenceValue);
    //             editor.OnInspectorGUI();
    //         }
    //     }
    // }
}
