using System.Text.RegularExpressions;

namespace CASCLib
{
    /// <summary>
    /// Represents a wildcard running on the
    /// <see cref="System.Text.RegularExpressions"/> engine.
    /// </summary>
    public class Wildcard : Regex
    {
        /// <summary>
        /// Initializes a wildcard with the given search pattern.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to match.</param>
        public Wildcard(string pattern, bool matchStartEnd)
         : base(WildcardToRegex(pattern, matchStartEnd))
        {
        }

        /// <summary>
        /// Initializes a wildcard with the given search pattern and options.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to match.</param>
        /// <param name="options">A combination of one or more
        /// <see cref="RegexOptions"/>.</param>
        public Wildcard(string pattern, bool matchStartEnd, RegexOptions options)
         : base(WildcardToRegex(pattern, matchStartEnd), options)
        {
        }

        /// <summary>
        /// Converts a wildcard to a regex.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to convert.</param>
        /// <returns>A regex equivalent of the given wildcard.</returns>
        public static string WildcardToRegex(string pattern, bool matchStartEnd)
        {
            if (matchStartEnd)
                return "^" + Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
        }
    }
}
