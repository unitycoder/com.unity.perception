using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace UnityEngine.Perception.GroundTruth
{
    /// <summary>
    /// A definition for how a <see cref="Labeling"/> should be resolved to a single label and id for ground truth generation.
    /// </summary>
    [CreateAssetMenu(fileName = "LabelingConfiguration2", menuName = "Perception/Labeling Configuration 2", order = 1)]
    public class LabelingConfiguration2 : ScriptableObject
    {
        /// <summary>
        /// Whether the inspector will auto-assign ids based on the id of the first element.
        /// </summary>
        public bool AutoAssignIds = true;

        /// <summary>
        /// Whether the inspector will start label ids at zero or one when <see cref="AutoAssignIds"/> is enabled.
        /// </summary>
        public StartingLabelId StartingLabelId = StartingLabelId.One;

        [SerializeField]
        public List<string> Labels = new List<string>();

        /// <summary>
        /// A sequence of <see cref="LabelEntry"/> which defines the labels relevant for this configuration and their values.
        /// </summary>
        [FormerlySerializedAs("LabelingConfigurations")]
        [SerializeField]
        public List<LabelFeature> LabelFeatures = new List<LabelFeature>();

        /// <summary>
        /// Attempts to find the matching index in <see cref="LabelEntries"/> for the given <see cref="Labeling"/>.
        /// </summary>
        /// <remarks>
        /// The matching index is the first class name in the given Labeling which matches an entry in <see cref="LabelEntries"/>.
        /// </remarks>
        /// <param name="labeling">The <see cref="Labeling"/> to match </param>
        /// <param name="labelEntry">When this method returns, contains the matching <see cref="LabelEntry"/>, or <code>default</code> if no match was found.</param>
        /// <returns>Returns true if a match was found. False if not.</returns>
        public bool TryGetMatchingConfigurationEntry(Labeling labeling, out LabelEntry labelEntry)
        {
            return TryGetMatchingConfigurationEntry(labeling, out labelEntry, out int _);
        }

        /// <summary>
        /// Attempts to find the matching index in <see cref="LabelEntries"/> for the given <see cref="Labeling"/>.
        /// </summary>
        /// <remarks>
        /// The matching index is the first class name in the given Labeling which matches an entry in <see cref="LabelEntries"/>.
        /// </remarks>
        /// <param name="labeling">The <see cref="Labeling"/> to match </param>
        /// <param name="labelEntry">When this method returns, contains the matching <see cref="LabelEntry"/>, or <code>default</code> if no match was found.</param>
        /// <param name="labelEntryIndex">When this method returns, contains the index of the matching <see cref="LabelEntry"/>, or <code>-1</code> if no match was found.</param>
        /// <returns>Returns true if a match was found. False if not.</returns>
        public bool TryGetMatchingConfigurationEntry(Labeling labeling, out LabelEntry labelEntry, out int labelEntryIndex)
        {
            foreach (var labelingClass in labeling.labels)
            {
                for (var i = 0; i < LabelEntries.Count; i++)
                {
                    var entry = LabelEntries[i];
                    if (string.Equals(entry.label, labelingClass))
                    {
                        labelEntry = entry;
                        labelEntryIndex = i;
                        return true;
                    }
                }
            }

            labelEntryIndex = -1;
            labelEntry = default;
            return false;
        }
    }

    /// <summary>
    /// Structure defining a label configuration for <see cref="LabelingConfiguration"/>.
    /// </summary>
    [Serializable]
    public class LabelFeature
    {
        public string key;
    }
    [Serializable]
    public class StringLabelFeature : LabelFeature
    {
        public List<string> values;
    }
    [Serializable]
    public class IntLabelFeature : LabelFeature
    {
        public List<string> values;
    }
    [Serializable]
    public class ColorLabelFeature : LabelFeature
    {
        public List<string> values;
    }
}
