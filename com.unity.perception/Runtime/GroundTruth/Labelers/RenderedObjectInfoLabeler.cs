using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Profiling;

namespace UnityEngine.Perception.GroundTruth
{
    [Serializable]
    public class RenderedObjectInfoLabeler : CameraLabeler
    {
        /// <summary>
        /// The ID to use for visible pixels metrics in the resulting dataset
        /// </summary>
        public string objectInfoMetricId = "5BA92024-B3B7-41A7-9D3F-C03A6A8DDD01";

        public LabelingConfiguration labelingConfiguration;

        static ProfilerMarker s_ProduceRenderedObjectInfoMetric = new ProfilerMarker("ProduceRenderedObjectInfoMetric");

        // ReSharper disable InconsistentNaming
        struct RenderedObjectInfoValue
        {
            [UsedImplicitly]
            public int label_id;
            [UsedImplicitly]
            public uint instance_id;
            [UsedImplicitly]
            public int visible_pixels;
        }
        // ReSharper restore InconsistentNaming

        RenderedObjectInfoValue[] m_VisiblePixelsValues;
        Dictionary<int, AsyncMetric> m_ObjectInfoAsyncMetrics;
        MetricDefinition m_RenderedObjectInfoMetricDefinition;

        public RenderedObjectInfoLabeler()
        {
        }
        public RenderedObjectInfoLabeler(LabelingConfiguration labelingConfiguration)
        {
            this.labelingConfiguration = labelingConfiguration;
        }

        public override void Setup()
        {
            if (labelingConfiguration == null)
            {
                Debug.LogError("labelingConfiguration must be assigned.");
                this.enabled = false;
                return;
            }

            m_ObjectInfoAsyncMetrics = new Dictionary<int, AsyncMetric>();

            perceptionCamera.RenderedObjectInfosCalculated += (frameCount, objectInfo) =>
            {
                ProduceRenderedObjectInfoMetric(objectInfo, frameCount);
            };
        }

        public override void OnBeginRendering()
        {
            if (m_RenderedObjectInfoMetricDefinition.Equals(default))
            {
                m_RenderedObjectInfoMetricDefinition = SimulationManager.RegisterMetricDefinition(
                    "rendered object info",
                    labelingConfiguration.GetAnnotationSpecification(),
                    "Information about each labeled object visible to the sensor",
                    id: new Guid(objectInfoMetricId));
            }

            m_ObjectInfoAsyncMetrics[Time.frameCount] = perceptionCamera.SensorHandle.ReportMetricAsync(m_RenderedObjectInfoMetricDefinition);
        }

        void ProduceRenderedObjectInfoMetric(NativeArray<RenderedObjectInfo> renderedObjectInfos, int frameCount)
        {
            using (s_ProduceRenderedObjectInfoMetric.Auto())
            {
                if (!m_ObjectInfoAsyncMetrics.TryGetValue(frameCount, out var metric))
                    return;

                m_ObjectInfoAsyncMetrics.Remove(frameCount);

                if (m_VisiblePixelsValues == null || m_VisiblePixelsValues.Length != renderedObjectInfos.Length)
                    m_VisiblePixelsValues = new RenderedObjectInfoValue[renderedObjectInfos.Length];

                for (var i = 0; i < renderedObjectInfos.Length; i++)
                {
                    var objectInfo = renderedObjectInfos[i];
                    if (!TryGetLabelEntryFromInstanceId(objectInfo.instanceId, out var labelEntry))
                        continue;

                    m_VisiblePixelsValues[i] = new RenderedObjectInfoValue
                    {
                        label_id = labelEntry.id,
                        instance_id = objectInfo.instanceId,
                        visible_pixels = objectInfo.pixelCount
                    };
                }

                metric.ReportValues(m_VisiblePixelsValues);
            }
        }

        bool TryGetLabelEntryFromInstanceId(uint instanceId, out LabelEntry labelEntry)
        {
            return labelingConfiguration.TryGetLabelEntryFromInstanceId(instanceId, out labelEntry);
        }
    }
}
