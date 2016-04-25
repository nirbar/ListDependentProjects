using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Panel.Software.ListDependentProjects
{
    /// <summary>
    /// Accumulate errors
    /// </summary>
    public static class ErrorManager
    {
        // Accumulated errors and error-source
        private static Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        /// <summary>
        /// -1 if any error occured. 0 otherwise
        /// </summary>
        public static int ExitCode
        {
            get
            {
                return Good ? 0 : -1;
            }
        }

        /// <summary>
        /// True if no error has occured, false otherwise
        /// </summary>
        public static bool Good
        {
            get
            {
                return !_errors.Any();
            }
        }

        /// <summary>
        /// Debug message
        /// </summary>
        /// <param name="msg"></param>
        public static void Debug(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
        }

        /// <summary>
        /// Debug message with format.
        /// </summary>
        /// <param name="msg"></param>
        public static void DebugF(string msg, params object[] args)
        {
            Debug(string.Format(msg, args));
        }

        /// <summary>
        /// Log an error with its source
        /// </summary>
        /// <param name="src">Source of the error</param>
        /// <param name="msg">Error message.</param>
        public static void Error(string src, string msg)
        {
            if (!_errors.ContainsKey(src))
            {
                _errors[src] = new List<string>();
            }

            _errors[src].Add(msg);

            Console.Error.WriteLine("Error in '{0}': {1}", src, msg);
        }

        /// <summary>
        /// Dump all errors to console error stream.
        /// </summary>
        public static void DumpErrors()
        {
            foreach (string s in _errors.Keys)
            {
                foreach (string m in _errors[s])
                {
                    Console.Error.WriteLine("Error in '{0}': {1}", s, m);
                }
            }
        }
    }
}
