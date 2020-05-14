using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Perception.Randomization;

namespace UnityEditor.Perception.Randomization
{
    [CustomEditor(typeof(Randomizer))]
    public class RandomizerEditor : Editor
    {
        struct RandomizationTarget
        {
            public GameObject GameObject;
            public Type ComponentType;
            public string FloatFieldName;
            public TargetKind TargetKind;
        }
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (EditorGUILayout.DropdownButton(new GUIContent("Add parameter"), FocusType.Keyboard))
            {
                var lastRect = GUILayoutUtility.GetLastRect();
                var dropdownOptions = GatherOptions().ToArray();
                var menu = new GenericMenu();
                foreach (var option in dropdownOptions)
                {
                    var localOption = option;
                    menu.AddItem(new GUIContent($"{option.GameObject.name}.{option.ComponentType.Name}.{option.FloatFieldName}"),
                        false,
                        () => AddParameter(localOption));
                }
                menu.DropDown(lastRect);
            }
        }

        void AddParameter(RandomizationTarget localOption)
        {
            var entriesProp = this.serializedObject.FindProperty(nameof(Randomizer.randomizationEntries));
            var newIndex = entriesProp.arraySize;
            entriesProp.InsertArrayElementAtIndex(newIndex);
            var element = entriesProp.GetArrayElementAtIndex(newIndex);
            element.FindPropertyRelative(nameof(RandomizationEntry.GameObject)).objectReferenceValue = localOption.GameObject;
            element.FindPropertyRelative(nameof(RandomizationEntry.ComponentType)).stringValue =
                $"{localOption.ComponentType.FullName}, {localOption.ComponentType.Assembly.FullName}";
            element.FindPropertyRelative(nameof(RandomizationEntry.MemberName)).stringValue = localOption.FloatFieldName;
            var targetKindProp = element.FindPropertyRelative(nameof(RandomizationEntry.TargetKind));
            targetKindProp.enumValueIndex = (int)localOption.TargetKind;
            element.FindPropertyRelative(nameof(RandomizationEntry.Min)).floatValue = 0f;
            element.FindPropertyRelative(nameof(RandomizationEntry.Max)).floatValue = 1f;
            serializedObject.ApplyModifiedProperties();
        }

        IEnumerable<RandomizationTarget> GatherOptions()
        {
            var randomizer = (Randomizer)this.target;
            var targetScene = randomizer.gameObject.scene;
            var rootGameObjects = targetScene.GetRootGameObjects();
            foreach (var rootGameObject in rootGameObjects)
            {
                foreach (var randomizationTarget in GatherOptions(rootGameObject.transform))
                {
                    yield return randomizationTarget;
                }
            }
        }

        IEnumerable<RandomizationTarget> GatherOptions(Transform parent)
        {
            foreach (var component in parent.GetComponents<Component>())
            {
                if (component == null)
                    continue;

                var componentType = component.GetType();
                var fieldInfos = componentType.GetFields();

                foreach (var fieldInfo in fieldInfos)
                {
                    if (fieldInfo.FieldType == typeof(float))
                        yield return new RandomizationTarget()
                        {
                            ComponentType = componentType,
                            FloatFieldName = fieldInfo.Name,
                            TargetKind = TargetKind.Field,
                            GameObject = parent.gameObject
                        };
                }
                var propertyInfos = componentType.GetProperties();

                foreach (var propertyInfo in propertyInfos)
                {
                    if (propertyInfo.PropertyType == typeof(float))
                        yield return new RandomizationTarget()
                        {
                            ComponentType = componentType,
                            FloatFieldName = propertyInfo.Name,
                            TargetKind = TargetKind.Property,
                            GameObject = parent.gameObject
                        };
                }
            }
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                foreach (var childOption in GatherOptions(parent.transform.GetChild(i)))
                {
                    yield return childOption;
                }
            }
        }
    }
}
