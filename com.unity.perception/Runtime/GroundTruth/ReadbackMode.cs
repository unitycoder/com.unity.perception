using System;

namespace UnityEngine.Perception.Sensors
{
    /// <summary>
    /// Types of GPU readback
    /// </summary>
    public enum ReadbackMode
    {
        /// <summary>
        /// Readback should occur asynchronously. The results may be retrieved many frames past the actual rendering.
        /// </summary>
        Async,
        /// <summary>
        /// Readback should occur synchronously. The results will always be retrieved in the same frame as rendering
        /// at the cost of performance.
        /// </summary>
        Synchronous
    }
}
