using System.Threading;

namespace SinglePass.WPF.Helpers
{
    /// <summary>
    /// Properties helper to operate with boolean values concurrently.
    /// </summary>
    public static class BooleanInterlocked
    {
        /// <summary>
        /// Get boolean.
        /// </summary>
        /// <param name="location1">Referenced integer field.</param>
        /// <returns><see langword="true"/> if referenced integer is 1, <see langword="false"/> if 0. </returns>
        public static bool Get(ref int location1)
        {
            return Interlocked.CompareExchange(ref location1, 1, 1) == 1;
        }

        /// <summary>
        /// Set boolean.
        /// </summary>
        /// <param name="location1">Referenced integer field.</param>
        /// <param name="newValue">New boolean value.</param>
        public static void Set(ref int location1, bool newValue)
        {
            if (newValue) Interlocked.CompareExchange(ref location1, 1, 0);
            else Interlocked.CompareExchange(ref location1, 0, 1);
        }
    }
}
