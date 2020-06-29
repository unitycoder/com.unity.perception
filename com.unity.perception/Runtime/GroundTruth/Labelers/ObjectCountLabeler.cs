using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Profiling;

namespace UnityEngine.Perception.GroundTruth
{
    [Serializable]
    public class ObjectCountLabeler : CameraLabeler
    {
        /// <summary>
        /// The ID to use for object count annotations in the resulting dataset
        /// </summary>
        public string objectCountMetricId = "51DA3C27-369D-4929-AEA6-D01614635CE2";

        public LabelingConfiguration labelingConfiguration => m_LabelingConfiguration;

        /// <summary>
        /// Fired when the object counts are computed for a frame.
        /// </summary>
        public event Action<int, NativeSlice<uint>,IReadOnlyList<LabelEntry>> ObjectCountsComputed;

        [SerializeField]
        LabelingConfiguration m_LabelingConfiguration;

        static ProfilerMarker s_ClassCountCallback = new ProfilerMarker("OnClassLabelsReceived");

        ClassCountValue[] m_ClassCountValues;

        Dictionary<int, AsyncMetric> m_ObjectCountAsyncMetrics;
        MetricDefinition m_ObjectCountMetricDefinition;

        public ObjectCountLabeler()
        {
        }
        public ObjectCountLabeler(LabelingConfiguration labelingConfiguration)
        {
            this.m_LabelingConfiguration = labelingConfiguration;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        struct ClassCountValue
        {
            public int label_id;
            public string label_name;
            public uint count;
        }

        public override void Setup()
        {
            if (labelingConfiguration == null)
                throw new InvalidOperationException("LabelingConfiguration must be supplied");

            m_ObjectCountAsyncMetrics =  new Dictionary<int, AsyncMetric>();

            perceptionCamera.RenderedObjectInfosCalculated += (frameCount, objectInfo) =>
            {
                NativeArray<uint> objectCounts = ComputeObjectCounts(objectInfo);
                ObjectCountsComputed?.Invoke(frameCount, objectCounts, labelingConfiguration.LabelEntries);
                ProduceObjectCountMetric(objectCounts, m_LabelingConfiguration.LabelEntries, frameCount);
            };
        }

        public override void OnBeginRendering()
        {
            if (m_ObjectCountMetricDefinition.Equals(default))
            {
                m_ObjectCountMetricDefinition = SimulationManager.RegisterMetricDefinition("object count", m_LabelingConfiguration.GetAnnotationSpecification(),
                    "Counts of objects for each label in the sensor's view", id: new Guid(objectCountMetricId));
            }

            m_ObjectCountAsyncMetrics[Time.frameCount] = perceptionCamera.SensorHandle.ReportMetricAsync(m_ObjectCountMetricDefinition);
        }

        NativeArray<uint> ComputeObjectCounts(NativeArray<RenderedObjectInfo> objectInfo)
        {
            var objectCounts = new NativeArray<uint>(m_LabelingConfiguration.LabelEntries.Count, Allocator.Temp);
            foreach (var info in objectInfo)
            {
                if (!m_LabelingConfiguration.TryGetLabelEntryFromInstanceId(info.instanceId, out _, out var labelIndex))
                    continue;

                objectCounts[labelIndex]++;
            }

            return objectCounts;
        }

        void ProduceObjectCountMetric(NativeSlice<uint> counts, IReadOnlyList<LabelEntry> entries, int frameCount)
        {
            using (s_ClassCountCallback.Auto())
            {
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
    }
}
