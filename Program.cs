using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace Panel.Software.ListDependentProjects
{
    class Program
    {
        /// <summary>
        /// Find projects that depend on a given project, and it's own project dependencies.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="/project">The name of the project</param>
        /// <param name="/folder">Root folder where projects that depend on the given project are searched for</param>
        /// <param name="/out">Optional. File to dump results to. If null then results will be dumped to Console.</param>
        /// <returns></returns>
        public static int Main(string[] args)
        {
            string projFile;
            string outFile;
            List<string> topFolders = new List<string>();
            if (!ParseCommandLine( topFolders, out projFile, out outFile))
            {
                Usage();
                return -1;
            }

            // Setup search folders and project pattern
            projFile = ParseProject.NormalizeReferenceName(projFile);
            SearchFolders folders = new SearchFolders();
            folders.AddFolders(topFolders);
            folders.AddPattern(projFile + ".*proj");
            ParseProject.SetSearchFolders(folders);

            // List the projects the target project depends on
            List<string> dependentProjects = new List<string>();
            foreach (string proj in folders.Search())
            {
                dependentProjects.Add(proj);
                using (ParseProject po = new ParseProject(proj))
                {
                    dependentProjects.AddRange(po.ProjectReferences);
                }
            }

            // List the projects that depend on the target project:
            // Find all project files in the folders.
            IEnumerable<string> projFiles = folders.Search("*.*proj");
            foreach (string p in projFiles)
            {
                using (ParseProject po = new ParseProject(p))
                {
                    // Test if this project depends on the target project
                    if (po.ProjectReferences.Contains(projFile, StringComparer.OrdinalIgnoreCase))
                    {
                        dependentProjects.Add(p);
                    }
                }
            }

            // Unique, sorted list
            if (ErrorManager.Good)
            {
                dependentProjects = new List<string>(dependentProjects.Distinct(StringComparer.OrdinalIgnoreCase));
                ParseProject.SetBuildOrder(dependentProjects);
            }

            // Dump project file, even if error(s) occured.
            if (!string.IsNullOrWhiteSpace(outFile))
            {
                ParseProject.CreateBuildFile(outFile, dependentProjects);
            }
            else
            {
                foreach (string s in dependentProjects)
                {
                    Console.WriteLine(s);
                }
            }

            return ErrorManager.ExitCode;
        }

        /// <summary>
        /// Print usage
        /// </summary>
        private static void Usage()
        {
            string usage = string.Format(
                "{0} /folder <Top Folder> /project <Project File Path> [/out <Output file name>]{1}{2}{3}"
                , Process.GetCurrentProcess().ProcessName
                , "\n\t/folder <Top Folder>:\tTop folder where dependent projects will be searched"
                , "\n\t/project <Project File Path>:\tProject for which dependent projects are tested"
                , "\n\t/out <Output file name>:\tFile to receive dependent projects list"
                );
            
            Console.WriteLine(usage);
            Console.WriteLine("Parse .NET project references");
        }

        /// <summary>
        /// Parse command line arguments
        /// </summary>
        /// <param name="topFolder"></param>
        /// <param name="projFile"></param>
        /// <param name="outputFile"></param>
        /// <returns>True if folder and project where specified. False otherwise</returns>
        private static bool ParseCommandLine(List<string> topFolders, out string projFile, out string outputFile)
        {
            projFile = null;
            outputFile = null;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; ++i) // Skip exe name
            {
                switch (args[i].ToLower())
                {
                    case "/folder":
                        if (i >= args.Length)
                        {
                            return false;
                        }

                        string folder = args[i + 1];
                        if (Directory.Exists(folder))
                        {
                            topFolders.Add(Path.GetFullPath(folder));
                        }

                        ++i;
                        break;

                    case "/project":
                        if (i >= args.Length)
                        {
                            return false;
                        }

                        projFile = args[i + 1];

                        ++i;
                        break;

                    case "/out":
                        if (i >= args.Length)
                        {
                            return false;
                        }

                        outputFile = args[i + 1];
                        if( outputFile.IndexOfAny( Path.GetInvalidPathChars()) >= 0)
                        {
                            return false;
                        }


                        ++i;
                        break;
                }
            }

            return ((topFolders.Count > 0) && (projFile != null));
        }
    }
}
