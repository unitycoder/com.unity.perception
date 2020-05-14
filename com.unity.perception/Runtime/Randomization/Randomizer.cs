using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace UnityEngine.Perception.Randomization
{
    public class Randomizer : MonoBehaviour
    {
        [SerializeField]
        public List<RandomizationEntry> randomizationEntries = new List<RandomizationEntry>();

        Unity.Mathematics.Random m_Random = new Unity.Mathematics.Random(1);

        void Update()
        {
            foreach (var randomizationEntry in randomizationEntries)
            {
                Randomize(randomizationEntry);
            }
        }

        void Randomize(RandomizationEntry randomizationEntry)
        {
            var value = m_Random.NextFloat(randomizationEntry.Min, randomizationEntry.Max);
            var type = Type.GetType(randomizationEntry.ComponentType);
            var component = randomizationEntry.GameObject.GetComponent(type);
            switch (randomizationEntry.TargetKind)
            {
                case TargetKind.Field:
                    var fieldInfo = type.GetField(randomizationEntry.MemberName);
                    fieldInfo.SetValue(component, value);
                    break;
                case TargetKind.Property:
                    var propertyInfo = type.GetProperty(randomizationEntry.MemberName);
                    propertyInfo.SetValue(component, value);
                    break;
            }
        }
    }

    public enum TargetKind
    {
        Field,
        Property
    }

    [Serializable]
    public class RandomizationEntry
    {
        public GameObject GameObject;
        public string ComponentType;
        public string MemberName;
        public TargetKind TargetKind;
        public float Min;
        public float Max;
    }
}
