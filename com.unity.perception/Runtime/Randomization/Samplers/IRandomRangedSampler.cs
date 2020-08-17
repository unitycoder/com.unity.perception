﻿namespace UnityEngine.Perception.Randomization.Samplers
{
    /// <summary>
    /// An interface describing bounded random samplers
    /// </summary>
    public interface IRandomRangedSampler
    {
        /// <summary>
        /// The base seed used to initialize this sampler's state
        /// </summary>
        uint baseSeed { get; set; }

        /// <summary>
        /// The current random state of this sampler
        /// </summary>
        uint state { get; set; }

        /// <summary>
        /// A range bounding the values generated by this sampler
        /// </summary>
        FloatRange range { get; set; }
    }
}
