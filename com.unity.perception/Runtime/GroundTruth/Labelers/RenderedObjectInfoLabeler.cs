using System;

namespace UnityEngine.Perception.GroundTruth
{
    [AddComponentMenu("Perception/Labelers/RenderedObjectInfoLabeler")]
    [RequireComponent(typeof(InstanceSegmentationLabeler))]
    public class RenderedObjectInfoLabeler : MonoBehaviour
    {
        public string annotationId = "F9F22E05-443F-4602-A422-EBE4EA9B55CB";
        public LabelingConfiguration labelingConfiguration;
    }
}
