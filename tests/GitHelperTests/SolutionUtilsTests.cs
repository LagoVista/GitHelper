using GitHelper.Build;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelperTests
{
    [TestClass]
    public class SolutionUtilsTests
    {
        const string rootPath = @"D:\NuvIoT";

        NugetUtils _nugetUtils;
        BuildUtils _buildUtils;
        NugetHelpers _nugetHelpers;
        SolutionHelper _solutionHelper;
        FileHelpers _fileHelpers;

        [TestInitialize]
        public void Init()
        {
            var consoleWriter = new ConsoleWriter();

            _fileHelpers = new FileHelpers(consoleWriter);

            _solutionHelper = new SolutionHelper(_fileHelpers, consoleWriter);
            _solutionHelper.LoadSolutions(rootPath);
            _buildUtils = new BuildUtils(new ConsoleWriter());
            _nugetHelpers = new NugetHelpers(consoleWriter, new FileHelpers(consoleWriter), _solutionHelper);
            _nugetUtils = new NugetUtils(consoleWriter, new FileHelpers(consoleWriter), _nugetHelpers );
        }

        [TestMethod]
        public void ReadSolutions()
        {
            var items = _solutionHelper.LoadSolutions(rootPath);
            foreach(var item in items.Result)
            {
                Console.WriteLine(item.Name + "," + item.LocalPath + "," + item.Repo);
                var projectFiles = _solutionHelper.GetAllProjectFiles(rootPath, item);
                foreach(var proj in projectFiles)
                {
                    Console.WriteLine(proj);
                }
                Console.WriteLine("----");
                Console.WriteLine();
            }
        }

        [TestMethod]
        public void RestoreSolution()
        {
            var items = _solutionHelper.LoadSolutions(rootPath);

            _buildUtils.Restore(rootPath, items.Result.First());
        }

        [TestMethod]
        public void RemoveOldPackages()
        {
            _nugetUtils.RemoveAllOldPackages(rootPath);
        }

        [TestMethod]
        public void UpdateNugetVersions()
        {
            var items = _solutionHelper.LoadSolutions(rootPath);
            var nugetVersion = _nugetHelpers.GenerateNugetVersion(1, 3, DateTime.Now);

            foreach (var solution in items.Result)
            {
                _nugetHelpers.ApplyToCSProjects(rootPath, solution, nugetVersion,new List<string>());
                _nugetHelpers.ApplyToAllNuspecFiles(rootPath, solution, nugetVersion);
            }
        }

        [TestMethod]
        public void BuildSolution()
        {
            var items = _solutionHelper.LoadSolutions(rootPath);

            _buildUtils.Build(rootPath, items.Result.First(), "release");
        }

        [TestMethod]
        public void CreatePackages()
        {
            var items = _solutionHelper.LoadSolutions(rootPath);

            _nugetUtils.CreatePackage(rootPath, items.Result.First());
        }

        private void FullBuild(SolutionInformation solution)
        {
            _buildUtils.Restore(rootPath, solution);
            _buildUtils.Build(rootPath, solution, "release");

            _nugetUtils.CreatePackage(rootPath, solution);
        }

        [TestMethod]
        public void BuildFirstTwoProjects()
        {
            _nugetUtils.RemoveAllOldPackages(rootPath);

            var items = _solutionHelper.LoadSolutions(rootPath);
            var nugetVersion = _nugetHelpers.GenerateNugetVersion(1, 3, DateTime.Now);

            _nugetHelpers.ApplyToCSProjects(rootPath, items.Result.First(), nugetVersion, new List<string>());
            _nugetHelpers.ApplyToAllNuspecFiles(rootPath, items.Result.First(), nugetVersion);

            _nugetHelpers.ApplyToCSProjects(rootPath, items.Result[1], nugetVersion, new List<string>());
            _nugetHelpers.ApplyToAllNuspecFiles(rootPath, items.Result[1], nugetVersion);

            FullBuild(items.Result[0]);
            FullBuild(items.Result[1]);
        }
    }
}
