using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.UIElements;

namespace UnityEngine.Perception.GroundTruth
{
    /// <summary>
    /// Cache of instance id -> label entry index for a LabelConfig. This is not well optimized and is the source of a known memory leak for apps that create new instances frequently
    /// </summary>
    class LabelEntryMatchCache : IGroundTruthGenerator, IDisposable
    {
        const int k_StartingObjectCount = 1 << 8;
        NativeList<ushort> m_InstanceIdToLabelEntryIndexLookup;
        LabelingConfiguration m_LabelingConfiguration;

        public LabelEntryMatchCache(LabelingConfiguration labelingConfiguration)
        {
            m_LabelingConfiguration = labelingConfiguration;
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<GroundTruthLabelSetupSystem>().Activate(this);
            m_InstanceIdToLabelEntryIndexLookup = new NativeList<ushort>(k_StartingObjectCount, Allocator.Persistent);
        }

        public bool TryGetLabelEntryFromInstanceId(uint instanceId, out LabelEntry labelEntry, out int index)
        {
            labelEntry = default;
            index = -1;
            if (m_InstanceIdToLabelEntryIndexLookup.Length <= instanceId)
                return false;

            index = m_InstanceIdToLabelEntryIndexLookup[(int)instanceId];
            labelEntry = m_LabelingConfiguration.LabelEntries[index];
            return true;
        }

        void IGroundTruthGenerator.SetupMaterialProperties(MaterialPropertyBlock mpb, Renderer renderer, Labeling labeling, uint instanceId)
        {
            if (m_LabelingConfiguration.TryGetMatchingConfigurationEntry(labeling, out _, out var index))
            {
                if (m_InstanceIdToLabelEntryIndexLookup.Length <= instanceId)
                {
                    m_InstanceIdToLabelEntryIndexLookup.Resize((int)instanceId + 1, NativeArrayOptions.ClearMemory);
                }
                m_InstanceIdToLabelEntryIndexLookup[(int)instanceId] = (ushort)index;
            }
        }

        public void Dispose()
        {
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<GroundTruthLabelSetupSystem>().Deactivate(this);
            m_InstanceIdToLabelEntryIndexLookup.Dispose();
        }
    }
}
