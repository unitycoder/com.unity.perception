using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Profiling;

namespace UnityEngine.Perception.GroundTruth
{
    [Serializable]
    public class BoundingBoxLabeler : CameraLabeler
    {
        public string annotationId = "F9F22E05-443F-4602-A422-EBE4EA9B55CB";

        static ProfilerMarker s_BoundingBoxCallback = new ProfilerMarker("OnBoundingBoxesReceived");

        PerceptionCamera m_PerceptionCamera;
        Dictionary<int, AsyncAnnotation> m_AsyncAnnotations = new Dictionary<int, AsyncAnnotation>();
        AnnotationDefinition m_BoundingBoxAnnotationDefinition;
        BoundingBoxValue[] m_BoundingBoxValues;
        RenderedObjectInfoLabeler m_RenderedObjectInfoLabeler;

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        struct BoundingBoxValue
        {
            public int label_id;
            public string label_name;
            public int instance_id;
            public float x;
            public float y;
            public float width;
            public float height;
        }

        void Start()
        {
            m_PerceptionCamera = GetComponent<PerceptionCamera>();
            m_RenderedObjectInfoLabeler = GetComponent<RenderedObjectInfoLabeler>();
            m_BoundingBoxAnnotationDefinition = SimulationManager.RegisterAnnotationDefinition("bounding box", m_RenderedObjectInfoLabeler.labelingConfiguration.GetAnnotationSpecification(),
                "Bounding box for each labeled object visible to the sensor", id: new Guid(annotationId));
            m_RenderedObjectInfoLabeler.renderedObjectInfosCalculated += OnRenderedObjectInfosCalculated;

            m_PerceptionCamera.BeginRendering += ReportAsyncMetrics;
        }

        void ReportAsyncMetrics()
        {
            m_AsyncAnnotations[Time.frameCount] = m_PerceptionCamera.SensorHandle.ReportAnnotationAsync(m_BoundingBoxAnnotationDefinition);
        }

        void OnRenderedObjectInfosCalculated(int frameCount, NativeArray<RenderedObjectInfo> renderedObjectInfos)
        {
            if (!m_AsyncAnnotations.TryGetValue(frameCount, out var asyncAnnotation))
                return;

            m_AsyncAnnotations.Remove(frameCount);

            using (s_BoundingBoxCallback.Auto())
            {
                if (m_BoundingBoxValues == null || m_BoundingBoxValues.Length != renderedObjectInfos.Length)
                    m_BoundingBoxValues = new BoundingBoxValue[renderedObjectInfos.Length];

                for (var i = 0; i < renderedObjectInfos.Length; i++)
                {
                    var objectInfo = renderedObjectInfos[i];
                    if (!m_RenderedObjectInfoLabeler.TryGetLabelEntryFromInstanceId(objectInfo.instanceId, out var labelEntry))
                        continue;

                    m_BoundingBoxValues[i] = new BoundingBoxValue
                    {
                        label_id = labelEntry.id,
                        label_name = labelEntry.label,
                        instance_id = objectInfo.instanceId,
                        x = objectInfo.boundingBox.x,
                        y = objectInfo.boundingBox.y,
                        width = objectInfo.boundingBox.width,
                        height = objectInfo.boundingBox.height,
                    };
                }

                asyncAnnotation.ReportValues(m_BoundingBoxValues);
            }
        }
    }
}
