using System;

namespace SmartReader
{
    /// <summary>Flags that sets how aggressively remove potentially useless content</summary>
    [Flags]
    public enum Flags
    {
        /// <summary>Do not perform any cleaning</summary>
        None = 0,
        /// <summary>Remove unlikely content</summary>
        StripUnlikelys = 1,
        /// <summary>Remove content according that does not pass a certain threshold</summary>
        WeightClasses = 2,
        /// <summary>Clean content that does not look promising</summary>
        CleanConditionally = 4
    }
}