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
    public class BuildUtils
    {
        IConsoleWriter _consoleWriter;
        public BuildUtils(IConsoleWriter writer)
        {
            _consoleWriter = writer;
        }

        public void Build(string rootPath, SolutionInformation solution, string configuration)
        {
            var solutionPath = Path.Combine(rootPath, solution.LocalPath);
            var solutionFile = Path.Combine(rootPath, solution.LocalPath, solution.Solution);

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build \"{solutionFile}\" -c {configuration}",
                    UseShellExecute = false,
                    WorkingDirectory = solutionPath,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.ErrorDataReceived += (sndr, msg) =>
            {
                var line = proc.StandardError.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Error, line);
            };

            proc.Start();

            while (!proc.StandardOutput.EndOfStream)
            {
                var line = proc.StandardOutput.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Message, line);
            }

            _consoleWriter.Flush(false);
        }

        public void Restore(string rootPath, SolutionInformation solution)
        {
            var solutionPath = Path.Combine(rootPath, solution.LocalPath);
            var nugetConfig = Path.Combine(rootPath, "nuget.config");

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo {
                    FileName = "dotnet",
                    Arguments = $"restore {solutionPath} --configfile {nugetConfig}  ",
                    UseShellExecute = false,
                    WorkingDirectory = solutionPath,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.ErrorDataReceived += (sndr, msg) =>
            {
                var line = proc.StandardError.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Error, line);
            };

            proc.Start();

            while (!proc.StandardOutput.EndOfStream)
            {
                var line = proc.StandardOutput.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Message, line);
            }

            _consoleWriter.Flush(false);

        }
    }
}
