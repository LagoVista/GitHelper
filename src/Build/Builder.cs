using LagoVista.Core.Validation;
using LagoVista.GitHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelper.Build
{
    public class Builder
    {
        private string _rootPath;
        private IConsoleWriter _writer;

        private SolutionHelper _solutionHelper;
        private NugetHelpers _nugetHelpers;
        private FileHelpers _fileHelper;

        private BuildUtils _buildUtils;
        private NugetUtils _nugetUtils;


        public Builder(string rootPath, IConsoleWriter writer)
        {
            _rootPath = rootPath;

            _writer = writer;
            _fileHelper = new FileHelpers(_writer);
            _solutionHelper = new SolutionHelper(_fileHelper, _writer);
            
            _nugetHelpers = new NugetHelpers(_writer, _fileHelper, _solutionHelper);

            _buildUtils = new BuildUtils(_writer);
            _nugetUtils = new NugetUtils(_writer, _fileHelper, _nugetHelpers);            
        }

        public InvokeResult BuildAll(string configuration, int minor, int major)
        {
            var processStart = DateTime.Now;

            var result  = _nugetUtils.RemoveAllOldPackages(_rootPath);
            if (!result.Successful) return result.ToInvokeResult();

            var solutionsResult = _solutionHelper.LoadSolutions(_rootPath);
            if (!solutionsResult.Successful) return solutionsResult.ToInvokeResult();
            var solutions = solutionsResult.Result;

            var nugetVersion = _nugetHelpers.GenerateNugetVersion(major, minor, DateTime.Now);

            foreach (var solution in solutions)
            {
                result = _nugetHelpers.ApplyToCSProjects(_rootPath, solution, nugetVersion);
                if (!result.Successful) return result.ToInvokeResult();

                result = _nugetHelpers.ApplyToAllNuspecFiles(_rootPath, solution, nugetVersion);
                if (!result.Successful) return result.ToInvokeResult();
            }

            _writer.Flush(true);

            var idx = 1;
            var totalCount = solutions.Where(sol => sol.Build).Count();
            foreach(var solution in solutions)
            {
                if (solution.Build)
                {
                    var start = DateTime.Now;
                    _writer.AddMessage(LogType.Message, $"Build started: {solution.Name} ({idx++} of {totalCount})");
                    _writer.AddMessage(LogType.Message, $"===============================================");
                    result = _buildUtils.Restore(_rootPath, solution);
                    if (!result.Successful) return result.ToInvokeResult();

                    result = _buildUtils.Build(_rootPath, solution, configuration);
                    if (!result.Successful) return result.ToInvokeResult();

                    result = _nugetUtils.CreatePackage(_rootPath, solution);
                    if (!result.Successful) return result.ToInvokeResult();

                    _writer.AddMessage(LogType.Success, $"Build Completed: {solution.Name} ({idx} of {totalCount})");
                    var buildTime = DateTime.Now - start;
                    var totalBuildTime = DateTime.Now - processStart;

                    _writer.AddMessage(LogType.Success, $"Current Build: {buildTime.Minutes}:{buildTime.Seconds:00}");
                    _writer.AddMessage(LogType.Success, $"Total        : {totalBuildTime.Minutes}:{totalBuildTime.Seconds:00}");
                    _writer.AddMessage(LogType.Success, $"");
                    _writer.Flush(true);
                }
            }

            _writer.AddMessage(LogType.Success, $"System Build Completed");
            _writer.AddMessage(LogType.Success, $"{DateTime.Now - processStart}");
            _writer.AddMessage(LogType.Success, $"");
            _writer.Flush();

            return InvokeResult.Success;

        }
    }
}
