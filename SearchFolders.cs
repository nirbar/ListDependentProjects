using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Panel.Software.ListDependentProjects
{
    /// <summary>
    /// Search for given patterns in given folders.
    /// Cache results for better performance.
    /// </summary>
    public class SearchFolders
    {
        private List<DirectoryInfo> _folders = new List<DirectoryInfo>();
        private List<string> _patterns = new List<string>();
        private Dictionary<string, List<string>> _cache = new Dictionary<string, List<string>>();

        public SearchFolders() { }

        /// <summary>
        /// Add a folder to the search
        /// Invalidates cache.
        /// </summary>
        /// <param name="folder"></param>
        public void AddFolder(string folder)
        {
            DirectoryInfo di = new DirectoryInfo(folder);
            if (di.Exists)
            {
                _cache = new Dictionary<string, List<string>>(); // Invalidate cache.
                _folders.Add(di);
            }
        }

        /// <summary>
        /// Add a set of folders to the search.
        /// Invalidates cache.
        /// </summary>
        /// <param name="folders"></param>
        public void AddFolders(IEnumerable<string> folders)
        {
            foreach (string f in folders)
            {
                AddFolder(f);
            }
        }

        /// <summary>
        /// Add pattern to search
        /// </summary>
        /// <param name="pattern"></param>
        public void AddPattern(string pattern)
        {
            _patterns.Add(pattern.ToLower());
        }

        /// <summary>
        /// Get search results for all pre-specified patterns.
        /// Results are cached for future queries.
        /// </summary>
        /// <returns>Distinct matches for all specified patterns in all specified folders. Returns cached results if available without checking for changes.</returns>
        public IEnumerable<string> Search()
        {
            List<string> matches = new List<string>();
            Dictionary<string, List<string>> newCache = new Dictionary<string, List<string>>();
            foreach (DirectoryInfo di in _folders)
            {
                foreach (string patt in _patterns)
                {
                    if (!newCache.ContainsKey(patt))
                    {
                        newCache[patt] = new List<string>();
                    }

                    ErrorManager.DebugF("Searching for '{0}' in '{1}'", patt, di.FullName);

                    // Pattern already in cache
                    if (_cache.ContainsKey(patt))
                    {
                        ErrorManager.Debug("Found cache...");
                        foreach (string s in _cache[patt])
                        {
                            ErrorManager.DebugF("\t'{0}'", s);
                        }

                        matches.AddRange(_cache[patt]);
                        continue;
                    }

                    // New search
                    FileInfo[] files = di.GetFiles(patt, SearchOption.AllDirectories);
                    foreach (FileInfo fi in files)
                    {
                        ErrorManager.DebugF("Found '{0}'", fi.FullName);

                        matches.Add(fi.FullName.ToLower());
                        newCache[patt].Add(fi.FullName.ToLower());
                    }
                }
            }

            // Append new results to cache
            _cache.Union(newCache);

            IEnumerable<string> retList = matches.Distinct();
            return retList;
        }

        /// <summary>
        /// Search for the specified pattern only. Doesn't search for the pre-specified patterns.
        /// Results are cached for future queries.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns>Distinct matches for the specified pattern in all specified folders. Returns cached results if available without checking for changes.</returns>
        public IEnumerable<string> Search(string pattern)
        {
            // Pattern already in cache?
            if (_cache.ContainsKey(pattern))
            {
                return _cache[pattern];
            }

            // New search
            List<string> matches = new List<string>();
            foreach (DirectoryInfo di in _folders)
            {
                FileInfo[] files = di.GetFiles(pattern, SearchOption.AllDirectories);
                foreach (FileInfo fi in files)
                {
                    matches.Add(fi.FullName.ToLower());
                }
            }

            // Cache.
            _cache.Add(pattern, new List<string>(matches.Distinct()));
            return _cache[pattern];
        }
    }
}
