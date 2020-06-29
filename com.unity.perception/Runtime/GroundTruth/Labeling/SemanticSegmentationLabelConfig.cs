using System;
using UnityEngine.Serialization;

namespace UnityEngine.Perception.GroundTruth {
    /// <summary>
    /// A definition for how a <see cref="Labeling"/> should be resolved to a single label and id for ground truth generation.
    /// </summary>
    [CreateAssetMenu(fileName = "SemanticSegmentationLabelingConfiguration", menuName = "Perception/Semantic Segmentation Label Config", order = 1)]
    public class SemanticSegmentationLabelConfig : LabelingConfiguration2<SemanticSegmentationLabelEntry>
    {
    }

    [Serializable]
    public struct SemanticSegmentationLabelEntry : ILabelEntry
    {
        string ILabelEntry.label => this.label;
        public string label;
        public Color color;
    }
}
