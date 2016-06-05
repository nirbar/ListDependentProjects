using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;

namespace Panel.Software.ListDependentProjects
{
    /// <summary>
    /// Class detects project references.
    /// Cache is used for improved performance.
    /// The class can search for project dependencies in a given <typeparamref name="Panel.Software.ListDependentProjects.SearchFolders"/> object.
    /// </summary>
    public class ParseProject : IDisposable
    {
        #region Private Fields

        // Microsoft MSBuild project
        private Project _msbuildProj = null;
        private ParseProject _myCache = null;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets a list of references and project references for this project.
        /// </summary>
        public List<string> AllReferences
        {
            get
            {
                // Already searched
                if (_allReferences != null)
                {
                    return _allReferences;
                }

                // Already cached.
                if (_myCache != null)
                {
                    return _myCache.AllReferences;
                }

                // Valid build.proj file exists?
                ParseBuildProj();
                if (_allReferences != null)
                {
                    return _allReferences;
                }

                _allReferences = new List<string>();
                _projReferences = new List<string>();
                if (_msbuildProj == null)
                {
                    return _allReferences;
                }

                foreach (ProjectItem pi in _msbuildProj.AllEvaluatedItems)
                {
                    switch (pi.ItemType)
                    {
                        case "Reference":

                            // Search for the dependency as project or as-is
                            string dllName = pi.EvaluatedInclude;
                            string dependency = dllName;

                            // Project?
                            if (!dllName.Equals("System", StringComparison.OrdinalIgnoreCase) 
                                && !dllName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                                && !dllName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                            {
                                dependency = SearchProject(dllName) ?? dllName;
                                if (dependency != dllName)
                                {
                                    ErrorManager.DebugF("Found reference's project file: '{0}' --> '{1}'", dllName, dependency);

                                    // Add refrence's refrences
                                    AddProjectReference(dependency);
                                    using (ParseProject po = new ParseProject(dependency))
                                    {
                                        _allReferences.AddRange(po.AllReferences);
                                        AddProjectReferences(po.ProjectReferences);
                                    }
                                }
                            }

                            ErrorManager.DebugF("Adding reference '{0}'", dependency);                            
                            _allReferences.Add(dependency);
                            break;

                        // Recursively parse the referenced project.
                        case "ProjectReference":
                            ErrorManager.DebugF("Adding project reference '{0}'", pi.EvaluatedInclude);
                            _allReferences.Add(pi.EvaluatedInclude);

                            // Get full path to project
                            string newPath = pi.EvaluatedInclude;
                            if (!Path.IsPathRooted(newPath))
                            {
                                FileInfo myFI = new FileInfo(this.FullPath);
                                newPath = Path.Combine(myFI.DirectoryName, pi.EvaluatedInclude);
                                newPath = Path.GetFullPath(newPath);
                            }

                            // Add refrence's refrences
                            AddProjectReference(newPath);
                            using (ParseProject po = new ParseProject(newPath))
                            {
                                _allReferences.AddRange(po.AllReferences);
                                AddProjectReferences(po.ProjectReferences);
                            }
                            break;

                        default:
                            break;
                    }
                }

                SetFileNames(_allReferences);
                MakeUnique(ref _allReferences);

                MakeUnique(ref _projReferences);

                ErrorManager.DebugF("Done parsing dependencies of '{0}'", _msbuildProj.FullPath);
                return _allReferences;
            }
        }
        private List<string> _allReferences = null;

        /// <summary>
        /// Get a list of project references as full path to project file.
        /// The list contains both explicit project references
        /// and references for which a project with an identical name was found.
        /// </summary>
        public List<string> ProjectReferences
        {
            get
            {
                // Already searched
                if (_projReferences != null)
                {
                    return _projReferences;
                }

                // Already cached.
                if (_myCache != null)
                {
                    return _myCache.ProjectReferences;
                }

                // Construct the loca _projReferences list.
                List<string> tmp = AllReferences;
                return _projReferences;
            }
        }
        private List<string> _projReferences = null;

        /// <summary>
        /// Get the full path to the project file.
        /// </summary>
        public string FullPath
        {
            get
            {
                if (_myCache != null)
                {
                    return _myCache.FullPath;
                }

                if (_msbuildProj != null)
                {
                    return _msbuildProj.FullPath;
                }

                return null;
            }
        }
        
        #endregion

        #region Public Methods

        /// <summary>
        /// C'tor. 
        /// </summary>
        /// <param name="projFile">Project file for which references will be listed.</param>
        public ParseProject(string projFile)
        {
            try
            {
                ErrorManager.DebugF("Parsing project '{0}' ...", projFile);
                
                _msbuildProj = new Project(projFile);

                // Check cache. Only valid project files get into the cache.
                _myCache = GetCache(_msbuildProj.FullPath);
                if (_myCache == null)
                {
                    _cache.Add(this);
                }
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException ex) // Probably a .vdproj file. ignoreable.
            {
                ErrorManager.DebugF("Exception '{0}' while parsing '{1}'", ex.GetType().FullName, projFile);
                _msbuildProj = null;
            }
        }

        /// <summary>
        /// Clear collection (otherwise we'll get exceptions).
        /// </summary>
        void IDisposable.Dispose()
        {
            if (_msbuildProj != null)
            {
                _msbuildProj.ProjectCollection.UnloadAllProjects();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Keep only file names (no path)
        /// </summary>
        /// <param name="list"></param>
        private void SetFileNames(List<string> list)
        {
            for (int i = list.Count - 1; i >= 0; --i)
            {
                string s = list[i];
                list.RemoveAt(i);

                string s2 = NormalizeReferenceName(s);
                list.Add(s2);

                ErrorManager.DebugF("Normalizing refrence name: '{0}' -> '{1}'", s, s2);
            }
        }

        /// <summary>
        /// Unique, sort list
        /// </summary>
        /// <param name="list"></param>
        private void MakeUnique(ref List<string> list)
        {
            list = new List<string>(list.Distinct(StringComparer.OrdinalIgnoreCase));
            list.Sort();
        }

        /// <summary>
        /// Attempt to find a project with the given name in the given search folders.
        /// </summary>
        /// <param name="projName">Name of the requested project, with or without .*proj extension</param>
        /// <returns></returns>
        private string SearchProject(string projName)
        {
            if (_searchFolders == null)
            {
                return null;
            }

            projName = NormalizeReferenceName(projName);
            if (!projName.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                projName += ".*proj";
            }

            IEnumerable<string> projects = _searchFolders.Search(projName);
            string proj = projects.FirstOrDefault();
            return proj;
        }

        /// <summary>
        /// Add a project reference to list. Check for circular dependencies.
        /// </summary>
        /// <param name="projPath"></param>
        private void AddProjectReference(string projPath)
        {
            if (this.FullPath.Equals(projPath, StringComparison.OrdinalIgnoreCase))
            {
                AddError("Circular dependency found in " + this.FullPath);
            }

            if (!_projReferences.Contains(projPath, StringComparer.OrdinalIgnoreCase))
            {
                _projReferences.Add(projPath);
            }
        }

        /// <summary>
        /// Add a project referencea to list. Check for circular dependencies.
        /// </summary>
        /// <param name="projPaths"></param>
        private void AddProjectReferences(List<string> projPaths)
        {
            if (projPaths.Contains(this.FullPath, StringComparer.OrdinalIgnoreCase))
            {
                AddError("Circular dependency found in " + this.FullPath);
            }

            _projReferences.AddRange(projPaths);
        }

        private void AddError(string msg)
        {
            ErrorManager.Error(this.FullPath, msg);
        }

        /// <summary>
        /// Parse a build.proj file, that already has all parsed dependent projects.
        /// Initially checks that build.proj file exists and had a newer modification time compared to the project file.
        /// </summary>
        private void ParseBuildProj()
        {
            if(_msbuildProj == null)
            {
                return;
            }

            // build.proj exists?
            string buildProjFile = Path.Combine(Path.GetDirectoryName(_msbuildProj.FullPath), "build.proj");
            if(!File.Exists(buildProjFile))
            {
                return;
            }

            // build.proj is newer than the project file?
            if (File.GetLastWriteTime(buildProjFile) < File.GetLastWriteTime(_msbuildProj.FullPath))
            {
                return;
            }

            // Parse it!
            _allReferences = new List<string>();
            _projReferences = new List<string>();
            Project buildProj = new Project(buildProjFile);
            ProjectTargetInstance tgt = buildProj.Targets["Build"];
            foreach(ProjectTaskInstance tsk in tgt.Tasks)
            {
                if(tsk.Name.Equals("MSBuild"))
                {
                    if(tsk.Parameters.Keys.Contains("Projects"))
                    {
                        // Once a build.proj includes our project we know that subsequent project depend on us.
                        string p = tsk.Parameters["Projects"];
                        if(Path.GetFileName(p).Equals(Path.GetFileName(_msbuildProj.FullPath), StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        _allReferences.Add(p);
                        AddProjectReference(p);
                    }
                }
            }
        }

        #endregion

        #region Static Private Fields

        // Project cache.
        private static List<ParseProject> _cache = new List<ParseProject>();

        // Set of folders to search for dependencies.
        private static SearchFolders _searchFolders = null;

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Return a normalized dependency name: 
        /// 'Waves.Common.AccountMgmtEngine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=eb0e586657aae229, processorArchitecture=MSIL'  ==> 'Waves.Common.AccountMgmtEngine'. 
        /// '..\folderName\Waves.Common.AccountMgmtEngine.csproj' ==> 'Waves.Common.AccountMgmtEngine'. 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string NormalizeReferenceName(string name)
        {
            string outName = name;
            try
            {
                int i = outName.IndexOf(',');
                if (i >= 0)
                {
                    outName = outName.Substring(0, i);
                }

                Regex rex = new Regex(@"^(?<refName>.*)\...proj$", RegexOptions.IgnoreCase);
                Match m = rex.Match(outName);
                if (m.Success)
                {
                    outName = m.Groups["refName"].Value;
                }

                FileInfo fi = new FileInfo(outName);
                outName = fi.Name;
            }
            catch { }

            return outName;
        }

        /// <summary>
        // Set of folders to search for dependencies.
        /// </summary>
        /// <param name="folders"></param>
        public static void SetSearchFolders(SearchFolders folders)
        {
            _searchFolders = folders;
        }

        /// <summary>
        /// Order the list in build-order.
        /// </summary>
        /// <param name="projectList">List of file paths to projects</param>
        /// <returns>True if build order was determined. False if there was a circular dependency.</returns>
        public static bool SetBuildOrder(List<string> projectList)
        {
            bool changed;
            int changeCount = 0;
            int maxChanges = projectList.Count * projectList.Count;

            do
            {
                if (changeCount > maxChanges)
                {
                    ErrorManager.Error("ParseProjects", "Project dependency dead-lock. There's a circular dependency in projects.");
                    return false;
                }
                changed = false;

                for (int i = 0; i < projectList.Count; ++i)
                {
                    string proj1 = projectList[i];
                    using (ParseProject po = new ParseProject(proj1))
                    {
                        for (int j = i + 1; j < projectList.Count; ++j)
                        {
                            string proj2 = projectList[j];
                            if (po.ProjectReferences.Contains(proj2, StringComparer.OrdinalIgnoreCase))
                            {
                                ErrorManager.DebugF("'{0}' depends on '{1}'.\n\tPutting '{1}' in index '{2}'\n\tPutting '{0}' in index '{3}'"
                                    , proj1
                                    , proj2
                                    , i
                                    , i + 1
                                    );

                                changed = true;
                                projectList.RemoveAt(j);
                                projectList.Insert(i, proj2);
                                break;
                            }
                        }
                    }

                    if (changed)
                    {
                        ++changeCount;
                        break;
                    }
                }
            } while (changed);

            return true;
        }

        /// <summary>
        /// Create a build file that build the projects in the specified order
        /// </summary>
        /// <param name="buildFilePath"></param>
        /// <param name="projectList"></param>
        /// <returns></returns>
        public static bool CreateBuildFile(string buildFilePath, List<string> projectList)
        {
            ProjectRootElement proj = ProjectRootElement.Create(buildFilePath);
            proj.AddProperty("Configuration", "Release");
            proj.AddProperty("Platform", "x86");


            ProjectTargetElement trgt = proj.AddTarget("Build");
            foreach(string p in projectList)
            {
                ProjectTaskElement task = trgt.AddTask("MSBuild");
                task.SetParameter("Projects", p);
            }

            proj.Save();
            return true;
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Search for this project in cache.
        /// </summary>
        /// <param name="projFile"></param>
        /// <returns></returns>
        private static ParseProject GetCache(string projFile)
        {
            foreach (ParseProject po in _cache)
            {
                if (po._msbuildProj.FullPath == projFile)
                {
                    ErrorManager.DebugF("Found '{0}' in cache", projFile);
                    return po;
                }
            }

            return null;
        }

        #endregion
    }
}
