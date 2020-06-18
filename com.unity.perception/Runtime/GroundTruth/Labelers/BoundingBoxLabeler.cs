using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Unity.Collections;
using Unity.Profiling;

namespace UnityEngine.Perception.GroundTruth
{
    [Serializable]
    public class BoundingBox2DLabeler : CameraLabeler
    {
        public string annotationId = "F9F22E05-443F-4602-A422-EBE4EA9B55CB";

        static ProfilerMarker s_BoundingBoxCallback = new ProfilerMarker("OnBoundingBoxesReceived");
        public LabelingConfiguration labelingConfiguration;

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

        public override void Setup()
        {
            m_RenderedObjectInfoLabeler = (RenderedObjectInfoLabeler)PerceptionCamera.labelers.First(l => l is RenderedObjectInfoLabeler && ((RenderedObjectInfoLabeler)l).labelingConfiguration == labelingConfiguration);
            if (m_RenderedObjectInfoLabeler == null)
            {
                PerceptionCamera.labelers
            }
            m_BoundingBoxAnnotationDefinition = SimulationManager.RegisterAnnotationDefinition("bounding box", m_RenderedObjectInfoLabeler.labelingConfiguration.GetAnnotationSpecification(),
                "Bounding box for each labeled object visible to the sensor", id: new Guid(annotationId));

            m_RenderedObjectInfoLabeler.renderedObjectInfosCalculated += OnRenderedObjectInfosCalculated;
        }

        public override void OnBeginRendering()
        {
            m_AsyncAnnotations[Time.frameCount] = PerceptionCamera.SensorHandle.ReportAnnotationAsync(m_BoundingBoxAnnotationDefinition);
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
