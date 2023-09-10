namespace Line_Reversal
{
    /// <summary>
    /// Collection of string related helper functions
    /// </summary>
    internal static class StringHelper
    {
        /// <summary>
        /// Reverses a given string and returns it
        /// </summary>
        /// <param name="s">the string that should be reversed</param>
        /// <returns>the string in reverse</returns>
        public static string Reverse(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        /// <summary>
        /// Splits a given string into chunks
        /// </summary>
        /// <param name="s">the string that should be split</param>
        /// <param name="chunkSize">the maximum size of the chunks</param>
        /// <returns>a list of chunks</returns>
        public static List<string> GetInChunks(string s, int chunkSize)
        {
            return s.Chunk(chunkSize)
                .Select(x => new string(x))
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }

        /// <summary>
        /// Escapes a given string according to the rules of the LRCP protocol
        /// </summary>
        /// <param name="s">the string that should be escaped</param>
        /// <returns>the escaped string</returns>
        public static string Escape(string s)
        {
            return s.Replace(@"\", @"\\").Replace("/", @"\/");
        }

        /// <summary>
        /// Unescapes a given string according to the rules of the LRCP protocol
        /// </summary>
        /// <param name="s">the string that should be unescaped</param>
        /// <returns>the unescaped string</returns>
        public static string Unescape(string s)
        {
            return s.Replace(@"\/", "/").Replace(@"\\", @"\");
        }

        /// <summary>
        /// Escapes a given string so that it can be printed to console (for debugging)
        /// </summary>
        /// <param name="s">the string that should be escaped</param>
        /// <returns>the escaped string</returns>
        public static string EscapeForConsole(string s)
        {
            return s.Replace("\n", "\\n");
        }
    }
}
