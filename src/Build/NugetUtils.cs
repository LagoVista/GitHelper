using LagoVista.GitHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelper.Build
{
    public class NugetUtils
    {
        IFileHelper _fileHelper;
        IConsoleWriter _consoleWriter;
        public NugetUtils(IConsoleWriter writer, IFileHelper fileHelpers)
        {
            _consoleWriter = writer;
        }

        public void CreatePackage(string rootPath, SolutionInformation solution)
        {
            var solutionPath = Path.Combine(rootPath, solution.LocalPath);
            var nugetFile = Path.Combine(rootPath, "nuget.exe");

            var nugetOutputDir = Path.Combine(rootPath, solution.Private ? "LocalPrivatePackages" : "LocalPackages");

            var nugetUtils = new NugetHelpers(_fileHelper);

            var getNuspecFiles = nugetUtils.GetAllNuspecFiles(rootPath, solution);
            foreach (var specFile in getNuspecFiles)
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = nugetFile,
                        Arguments = $"pack -OutputDirectory {nugetOutputDir} {specFile}",
                        UseShellExecute = false,
                        WorkingDirectory = solutionPath,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                proc.Start();

                while (!proc.StandardError.EndOfStream)
                {
                    var line = proc.StandardError.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Error, line);
                }

                while (!proc.StandardOutput.EndOfStream)
                {
                    var line = proc.StandardOutput.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Message, line);
                }
                _consoleWriter.Flush(false);
            }
        }


        public void RemoveAllOldPackages(string rootPath)
        {
            var publicNugetOutputDir = Path.Combine(rootPath, "LocalPackages");
            var privateNugetOutputDir = Path.Combine(rootPath, "LocalPrivatePackages");

            var dirInfo = new DirectoryInfo(publicNugetOutputDir);
            foreach (var file in dirInfo.GetFiles()) 
            {
                file.Delete();
            }

            dirInfo = new DirectoryInfo(privateNugetOutputDir);
            foreach (var file in dirInfo.GetFiles())
            {
                file.Delete();
            }
        }
    }
}
