using LagoVista.Core.Validation;
using LagoVista.GitHelper;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GitHelper.Build
{
    public class BuildUtils
    {
        IConsoleWriter _consoleWriter;
        public BuildUtils(IConsoleWriter writer)
        {
            _consoleWriter = writer;
        }

        public InvokeResult Build(string rootPath, SolutionInformation solution, string configuration)
        {
            var solutionPath = Path.Combine(rootPath, solution.LocalPath);
            var solutionFile = Path.Combine(rootPath, solution.LocalPath, solution.Solution);

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build \"{solutionFile}\" -v m --no-incremental -c {configuration}",
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
                errs.Append("Build Failed!");
            }

            _consoleWriter.Flush(false);

            if (String.IsNullOrEmpty(errs.ToString()))
            {
                return InvokeResult.Success;
            }
            else
            {
                return InvokeResult.FromError(errs.ToString());
            }
        }

        public InvokeResult Restore(string rootPath, SolutionInformation solution)
        {
            var solutionPath = Path.Combine(rootPath, solution.LocalPath);
            var nugetConfig = Path.Combine(rootPath, "nuget.config");

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"restore {solutionPath}\\{solution.Solution} --configfile {nugetConfig}  ",
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
                errs.Append("Restore Failed!");
            }

            _consoleWriter.Flush(false);

            if (String.IsNullOrEmpty(errs.ToString()))
            {
                return InvokeResult.Success;
            }
            else
            {
                return InvokeResult.FromError(errs.ToString());
            }
        }
    }
}
