using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Rendering;

namespace UnityEditor.Perception.GroundTruth
{
    [CustomEditor(typeof(PerceptionCamera))]
    public class PerceptionCameraEditor : Editor
    {
        ReorderableList m_LabelersList;

        SerializedProperty labelersProperty => this.serializedObject.FindProperty("m_Labelers");
        public void OnEnable()
        {
            m_LabelersList = new ReorderableList(this.serializedObject, labelersProperty, true, false, true, true);
            m_LabelersList.drawHeaderCallback = (rect) => {
                EditorGUI.LabelField(rect, "Camera Labelers", EditorStyles.largeLabel);
            };
            m_LabelersList.elementHeightCallback = GetElementHeight;
            m_LabelersList.drawElementCallback = DrawElement;
            m_LabelersList.onAddCallback += OnAdd;
            m_LabelersList.onRemoveCallback += OnRemove;
        }

        float GetElementHeight(int index)
        {
            var serializedProperty = labelersProperty;
            var element = serializedProperty.GetArrayElementAtIndex(index);
            var editor = GetCameraLabelerDrawer(element, index);
            return editor.GetElementHeight(element);
        }

        PerceptionCamera PerceptionCamera => ((PerceptionCamera)this.target);

        void DrawElement(Rect rect, int index, bool isactive, bool isfocused)
        {
            var element = labelersProperty.GetArrayElementAtIndex(index);
            var editor = GetCameraLabelerDrawer(element, index);
            editor.OnGUI(rect, element);
        }

        void OnRemove(ReorderableList list)
        {
            var labelers = serializedObject.FindProperty(nameof(PerceptionCamera.labelers));
            labelers.DeleteArrayElementAtIndex(list.index);
            serializedObject.ApplyModifiedProperties();
        }

        void OnAdd(ReorderableList list)
        {
            Undo.RegisterCompleteObjectUndo(target, "Remove camera labeler");
            var labelers = labelersProperty;

            var dropdownOptions = TypeCache.GetTypesDerivedFrom<CameraLabeler>();
            var menu = new GenericMenu();
            foreach (var option in dropdownOptions)
            {
                var localOption = option;
                menu.AddItem(new GUIContent(option.Name),
                    false,
                    () => AddLabeler(labelers, localOption));
            }
            menu.ShowAsContext();
        }

        void AddLabeler(SerializedProperty labelers, Type labelerType)
        {
            var insertIndex = labelers.arraySize;
            labelers.InsertArrayElementAtIndex(insertIndex);
            var element = labelers.GetArrayElementAtIndex(insertIndex);
            var labeler = (CameraLabeler) Activator.CreateInstance(labelerType);
            labeler.enabled = true;
            element.managedReferenceValue = labeler;
            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            if (EditorSettings.asyncShaderCompilation)
            {
                EditorGUILayout.HelpBox("Asynchronous shader compilation is currently enabled.", MessageType.Warning);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.description)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.period)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.startTime)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.captureRgbImages)));
            //EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PerceptionCamera.labelers)));
            m_LabelersList.DoLayoutList();
        }

        Dictionary<SerializedProperty, CameraLabelerDrawer> cameraLabelerDrawers = new Dictionary<SerializedProperty, CameraLabelerDrawer>();
        CameraLabelerDrawer GetCameraLabelerDrawer(SerializedProperty element, int listIndex)
        {
            CameraLabelerDrawer drawer;

            if (cameraLabelerDrawers.TryGetValue(element, out drawer))
                return drawer;

            var labeler = PerceptionCamera.labelers[listIndex];

            foreach (var drawerType in TypeCache.GetTypesWithAttribute(typeof(CameraLabelerDrawerAttribute)))
            {
                var attr = drawerType.GetCustomAttributes(typeof(CameraLabelerDrawerAttribute), true)[0] as CameraLabelerDrawerAttribute;
                if (attr.targetLabelerType == labeler.GetType())
                {
                    drawer = Activator.CreateInstance(drawerType) as CameraLabelerDrawer;
                    drawer.CameraLabeler = labeler;
                    break;
                }
                if (attr.targetLabelerType.IsAssignableFrom(labeler.GetType()))
                {
                    drawer = Activator.CreateInstance(drawerType) as CameraLabelerDrawer;
                    drawer.CameraLabeler = labeler;
                }
            }

            cameraLabelerDrawers[element] = drawer;

            return drawer;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    class CameraLabelerDrawerAttribute : Attribute
    {
        public CameraLabelerDrawerAttribute(Type targetLabelerType)
        {
            this.targetLabelerType = targetLabelerType;
        }
        public Type targetLabelerType;
    }

    [CameraLabelerDrawer(typeof(CameraLabeler))]
    public class CameraLabelerDrawer
    {
        public  CameraLabeler CameraLabeler { get; internal set; }

        class Styles
	    {
		    public static float defaultLineSpace = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            public static float reorderableListHandleIndentWidth = 12;
			public static GUIContent enabled = new GUIContent("Enabled", "Enable or Disable the custom pass");
	    }

	    bool firstTime = true;

	    // Serialized Properties
		SerializedProperty      	m_Enabled;
		SerializedProperty      	m_LabelerFoldout;
		List<SerializedProperty>	m_CustomPassUserProperties = new List<SerializedProperty>();

		void FetchProperties(SerializedProperty property)
		{
			m_Enabled = property.FindPropertyRelative(nameof(CameraLabeler.enabled));
			m_LabelerFoldout = property.FindPropertyRelative(nameof(CameraLabeler.foldout));
		}

		void LoadUserProperties(SerializedProperty customPass)
		{
			foreach (var field in CameraLabeler.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				var serializeField = field.GetCustomAttribute<SerializeField>();
				var hideInInspector = field.GetCustomAttribute<HideInInspector>();
				var nonSerialized = field.GetCustomAttribute<NonSerializedAttribute>();

				if (nonSerialized != null || hideInInspector != null)
					continue;

				if (!field.IsPublic && serializeField == null)
					continue;

                if (field.Name == nameof(CameraLabeler.enabled) || field.Name == nameof(CameraLabeler.foldout))
                    continue;

				var prop = customPass.FindPropertyRelative(field.Name);
				if (prop != null)
					m_CustomPassUserProperties.Add(prop);
			}
		}

	    void InitInternal(SerializedProperty customPass)
	    {
			FetchProperties(customPass);
			Initialize(customPass);
			LoadUserProperties(customPass);
		    firstTime = false;
	    }

		/// <summary>
		/// Use this function to initialize the local SerializedProperty you will use in your pass.
		/// </summary>
		/// <param name="customPass">Your custom pass instance represented as a SerializedProperty</param>
		protected virtual void Initialize(SerializedProperty customPass) {}

        internal void OnGUI(Rect rect, SerializedProperty property)
	    {
			rect.height = EditorGUIUtility.singleLineHeight;
			EditorGUI.BeginChangeCheck();

			if (firstTime)
			    InitInternal(property);

			DoHeaderGUI(ref rect);

			if (m_LabelerFoldout.boolValue)
			{
				EditorGUI.EndChangeCheck();
				return;
			}

			EditorGUI.BeginDisabledGroup(!m_Enabled.boolValue);
			{
				DoPassGUI(property, rect);
			}
			EditorGUI.EndDisabledGroup();

			if (EditorGUI.EndChangeCheck())
				property.serializedObject.ApplyModifiedProperties();
	    }

        /// <summary>
		/// Implement this function to draw your custom GUI.
		/// </summary>
		/// <param name="customPass">Your custom pass instance represented as a SerializedProperty</param>
		/// <param name="rect">space available for you to draw the UI</param>
		protected virtual void DoPassGUI(SerializedProperty customPass, Rect rect)
		{
			foreach (var prop in m_CustomPassUserProperties)
			{
				EditorGUI.PropertyField(rect, prop);
				rect.y += Styles.defaultLineSpace;
			}
		}

		void DoHeaderGUI(ref Rect rect)
		{
			var enabledSize = EditorStyles.boldLabel.CalcSize(Styles.enabled) + new Vector2(Styles.reorderableListHandleIndentWidth, 0);
			var headerRect = new Rect(rect.x + Styles.reorderableListHandleIndentWidth,
							rect.y + EditorGUIUtility.standardVerticalSpacing,
							rect.width - Styles.reorderableListHandleIndentWidth - enabledSize.x,
							EditorGUIUtility.singleLineHeight);
			rect.y += Styles.defaultLineSpace;
			var enabledRect = headerRect;
			enabledRect.x = rect.xMax - enabledSize.x;
			enabledRect.width = enabledSize.x;

			m_LabelerFoldout.boolValue = EditorGUI.Foldout(headerRect, m_LabelerFoldout.boolValue, $"{CameraLabeler.GetType().Name}", true, EditorStyles.boldLabel);
			EditorGUIUtility.labelWidth = enabledRect.width - 14;
			m_Enabled.boolValue = EditorGUI.Toggle(enabledRect, Styles.enabled, m_Enabled.boolValue);
			EditorGUIUtility.labelWidth = 0;
		}

		/// <summary>
		/// Implement this functions if you implement DoPassGUI. The result of this function must match the number of lines displayed in your custom GUI.
		/// Note that this height can be dynamic.
		/// </summary>
		/// <param name="customPass">Your custom pass instance represented as a SerializedProperty</param>
		/// <returns>The height in pixels of tour custom pass GUI</returns>
		protected virtual float GetHeight(SerializedProperty customPass)
		{
			float height = 0;

			foreach (var prop in m_CustomPassUserProperties)
			{
				height += EditorGUI.GetPropertyHeight(prop);
				height += EditorGUIUtility.standardVerticalSpacing;
			}

			return height;
		}

	    internal float GetElementHeight(SerializedProperty property)
	    {
		    float height = Styles.defaultLineSpace;

			if (firstTime)
				InitInternal(property);

			if (m_LabelerFoldout.boolValue)
				return height;

            return height + GetHeight(property);
	    }
    }
}
