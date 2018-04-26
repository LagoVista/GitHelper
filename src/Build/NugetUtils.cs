using LagoVista.Core.Validation;
using LagoVista.GitHelper;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GitHelper.Build
{
    public class NugetUtils
    {
        IFileHelper _fileHelper;
        IConsoleWriter _consoleWriter;
        NugetHelpers _nugetHelpers;
        public NugetUtils(IConsoleWriter writer, IFileHelper fileHelper, NugetHelpers nugetHelpers)
        {
            _nugetHelpers = nugetHelpers;
            _fileHelper = fileHelper;
            _consoleWriter = writer;
        }

        public InvokeResult CreatePackage(string rootPath, SolutionInformation solution)
        {
            var solutionPath = Path.Combine(rootPath, solution.LocalPath);
            var nugetFile = Path.Combine(rootPath, "nuget.exe");

            var nugetOutputDir = Path.Combine(rootPath, solution.Private ? "LocalPrivatePackages" : "LocalPackages");

            var getNuspecFiles = _nugetHelpers.GetAllNuspecFiles(rootPath, solution);
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

                var errs = new StringBuilder();

                while (!proc.StandardOutput.EndOfStream)
                {
                    var line = proc.StandardOutput.ReadLine().Trim();                    
                    _consoleWriter.AddMessage(LogType.Message, line);
                }

                while (!proc.StandardError.EndOfStream)
                {
                    var line = proc.StandardError.ReadLine().Trim();
                    errs.Append(line);
                    _consoleWriter.AddMessage(LogType.Error, line);
                }

                if (proc.ExitCode != 0)
                {
                    errs.Append("Packaging Failed!");
                }

                _consoleWriter.Flush(false);

                if (!String.IsNullOrEmpty(errs.ToString()))
                {
                    return InvokeResult.FromError(errs.ToString());
                }
            }

            return InvokeResult.Success;
        }


        public InvokeResult RemoveAllOldPackages(string rootPath)
        {
            var publicNugetOutputDir = Path.Combine(rootPath, "LocalPackages");
            var privateNugetOutputDir = Path.Combine(rootPath, "LocalPrivatePackages");

            var dirInfo = new DirectoryInfo(publicNugetOutputDir);
            foreach (var file in dirInfo.GetFiles())
            {
                try
                {
                    file.Delete();
                    _consoleWriter.AddMessage(LogType.Message, $"Deleted " + file.Name);
                }
                catch (Exception)
                {
                    _consoleWriter.AddMessage(LogType.Error, "Could not delete file: " + file.FullName);
                    return InvokeResult.FromError("Could not delete file: " + file.FullName);
                }
            }

            dirInfo = new DirectoryInfo(privateNugetOutputDir);
            foreach (var file in dirInfo.GetFiles())
            {
                try
                {
                    file.Delete();
                    _consoleWriter.AddMessage(LogType.Message, $"Deleted " + file.Name);
                }
                catch (Exception)
                {
                    _consoleWriter.AddMessage(LogType.Error, "Could not delete file: " + file.FullName);
                    return InvokeResult.FromError("Could not delete file: " + file.FullName);
                }
            }

            _consoleWriter.Flush(false);

            return InvokeResult.Success;
        }
    }
}
