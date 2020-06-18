using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine.Serialization;

namespace UnityEngine.Perception.GroundTruth
{
    [Serializable]
    public class RenderedObjectInfoLabeler : CameraLabeler
    {
        public bool produceObjectInfoMetrics = true;
        /// <summary>
        /// The ID to use for visible pixels metrics in the resulting dataset
        /// </summary>
        public string objectInfoMetricId = "5BA92024-B3B7-41A7-9D3F-C03A6A8DDD01";
        public bool produceObjectCountMetrics = true;
        /// <summary>
        /// The ID to use for object count annotations in the resulting dataset
        /// </summary>
        public string objectCountMetricId = "51DA3C27-369D-4929-AEA6-D01614635CE2";

        public LabelingConfiguration labelingConfiguration;

        static ProfilerMarker s_RenderedObjectInfosCalculatedEvent = new ProfilerMarker("renderedObjectInfosCalculated event");
        static ProfilerMarker s_ClassCountCallback = new ProfilerMarker("OnClassLabelsReceived");
        static ProfilerMarker s_ProduceRenderedObjectInfoMetric = new ProfilerMarker("ProduceRenderedObjectInfoMetric");

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        struct ObjectCountSpec
        {
            [UsedImplicitly]
            public int label_id;
            [UsedImplicitly]
            public string label_name;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        struct ClassCountValue
        {
            public int label_id;
            public string label_name;
            public uint count;
        }

        // ReSharper disable InconsistentNaming
        struct RenderedObjectInfoValue
        {
            [UsedImplicitly]
            public int label_id;
            [UsedImplicitly]
            public int instance_id;
            [UsedImplicitly]
            public int visible_pixels;
        }
        // ReSharper restore InconsistentNaming

        RenderedObjectInfoValue[] m_VisiblePixelsValues;
        ClassCountValue[] m_ClassCountValues;

        Dictionary<int, AsyncMetric> m_ObjectCountAsyncMetrics = new Dictionary<int, AsyncMetric>();
        Dictionary<int, AsyncMetric> m_ObjectInfoAsyncMetrics = new Dictionary<int, AsyncMetric>();
        MetricDefinition m_ObjectCountMetricDefinition;
        MetricDefinition m_RenderedObjectInfoMetricDefinition;

        internal event Action<NativeSlice<uint>, IReadOnlyList<LabelEntry>, int> classCountsReceived;

        public string boundingBoxAnnotationId = "F9F22E05-443F-4602-A422-EBE4EA9B55CB";

        Dictionary<int, AsyncAnnotation> m_AsyncAnnotations = new Dictionary<int, AsyncAnnotation>();
        AnnotationDefinition m_BoundingBoxAnnotationDefinition;
        BoundingBoxValue[] m_BoundingBoxValues;

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

        public void Setup()
        {
            if (labelingConfiguration == null)
            {
                Debug.LogError("labelingConfiguration must be assigned.");
                this.enabled = false;
                return;
            }


            PerceptionCamera.InstanceSegmentationImageReadback += (frameCount, data, tex) =>
            {

                using (s_RenderedObjectInfosCalculatedEvent.Auto())
                    renderedObjectInfosCalculated?.Invoke(frameCount, renderedObjectInfos);

                if (produceObjectCountMetrics)
                    ProduceObjectCountMetric(classCounts, labelingConfiguration.LabelEntries, frameCount);

                if (produceObjectInfoMetrics)
                    ProduceRenderedObjectInfoMetric(renderedObjectInfos, frameCount);
            };
        }

        public override void OnBeginRendering()
        {
            if (produceObjectCountMetrics)
            {
                if (m_ObjectCountMetricDefinition.Equals(default(MetricDefinition)))
                {
                    m_ObjectCountMetricDefinition = SimulationManager.RegisterMetricDefinition("object count", CreateLabelingMetricSpecs(),
                        "Counts of objects for each label in the sensor's view", id: new Guid(objectCountMetricId));
                }

                m_ObjectCountAsyncMetrics[Time.frameCount] = m_PerceptionCamera.SensorHandle.ReportMetricAsync(m_ObjectCountMetricDefinition);
            }
            if (produceObjectInfoMetrics)
            {
                if (m_RenderedObjectInfoMetricDefinition.Equals(default(MetricDefinition)))
                {
                    m_RenderedObjectInfoMetricDefinition = SimulationManager.RegisterMetricDefinition(
                        "rendered object info",
                        CreateLabelingMetricSpecs(),
                        "Information about each labeled object visible to the sensor",
                        id: new Guid(objectInfoMetricId));
                }

                m_ObjectInfoAsyncMetrics[Time.frameCount] = m_PerceptionCamera.SensorHandle.ReportMetricAsync(m_RenderedObjectInfoMetricDefinition);
            }
        }

        ObjectCountSpec[] CreateLabelingMetricSpecs()
        {
            var labelingMetricSpec = labelingConfiguration.LabelEntries.Select((l) => new ObjectCountSpec()
            {
                label_id = l.id,
                label_name = l.label,
            }).ToArray();
            return labelingMetricSpec;
        }

        void ProduceObjectCountMetric(NativeSlice<uint> counts, IReadOnlyList<LabelEntry> entries, int frameCount)
        {
            using (s_ClassCountCallback.Auto())
            {
                classCountsReceived?.Invoke(counts, entries, frameCount);

                if (!m_ObjectCountAsyncMetrics.TryGetValue(frameCount, out var classCountAsyncMetric))
                    return;

                m_ObjectCountAsyncMetrics.Remove(frameCount);

                if (m_ClassCountValues == null || m_ClassCountValues.Length != entries.Count)
                    m_ClassCountValues = new ClassCountValue[entries.Count];

                for (var i = 0; i < entries.Count; i++)
                {
                    m_ClassCountValues[i] = new ClassCountValue()
                    {
                        label_id = entries[i].id,
                        label_name = entries[i].label,
                        count = counts[i]
                    };
                }

                classCountAsyncMetric.ReportValues(m_ClassCountValues);
            }
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

        public bool TryGetLabelEntryFromInstanceId(int instanceId, out LabelEntry labelEntry)
        {
            return m_RenderedObjectInfoGenerator.TryGetLabelEntryFromInstanceId(instanceId, out labelEntry);
        }

        void OnDisable()
        {
            if (m_RenderedObjectInfoGenerator != null)
            {
                World.DefaultGameObjectInjectionWorld?.GetExistingSystem<GroundTruthLabelSetupSystem>()?.Deactivate(m_RenderedObjectInfoGenerator);
                m_RenderedObjectInfoGenerator?.Dispose();
                m_RenderedObjectInfoGenerator = null;
            }
        }
    }
}
