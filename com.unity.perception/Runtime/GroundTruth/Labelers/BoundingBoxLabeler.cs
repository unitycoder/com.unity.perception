using System;
using System.Linq;

namespace UnityEngine.Perception.GroundTruth {
    [Serializable]
    public class BoundingBoxLabeler : CameraLabeler
    {
        public string annotationId = "F9F22E05-443F-4602-A422-EBE4EA9B55CB";
        public LabelingConfiguration labelingConfiguration;
        AnnotationDefinition m_BoundingBoxAnnotationDefinition;

        public override void Setup()
        {
            var labelingMetricSpec = labelingConfiguration.GetAnnotationSpecification();
            m_BoundingBoxAnnotationDefinition = SimulationManager.RegisterAnnotationDefinition("bounding box", labelingMetricSpec, "Bounding box for each labeled object visible to the sensor", id: new Guid(annotationId));
        }

        public override void Update()
        {
            //BoundingBoxAsyncMetric = SensorHandle.ReportAnnotationAsync(m_BoundingBoxAnnotationDefinition);
        }

        public override void OnInstanceSegmentationRead(int frameCount)
        {
        }
    }
}
