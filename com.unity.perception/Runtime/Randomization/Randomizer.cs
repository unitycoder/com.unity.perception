using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Perception.Randomization
{
    public class Randomizer : MonoBehaviour
    {
        [SerializeField]
        public List<RandomizationEntry> randomizationEntries = new List<RandomizationEntry>();

        void Update()
        {
            randomizationEntries
        }
    }

    public class RandomizationEntry
    {
        public GameObject GameObject;
        public Type ComponentType;
        public string MemberName;
        public float Min;
        public float Max;
    }
}
