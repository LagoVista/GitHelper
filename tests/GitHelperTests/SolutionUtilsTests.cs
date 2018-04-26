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
        SolutionsHelper _solutionHelper;

        [TestInitialize]
        public void Init()
        {
            var helper = new SolutionsHelper();
            var items = helper.LoadSolutions(rootPath);
            _buildUtils = new BuildUtils(new ConsoleWriter());
            _nugetUtils = new NugetUtils(new ConsoleWriter(), new FileHelpers());
            _solutionHelper = new SolutionsHelper();

            _nugetHelpers = new NugetHelpers(new FileHelpers());
        }

        [TestMethod]
        public void ReadSolutions()
        {
            var helper = new SolutionsHelper();
            var items = helper.LoadSolutions(rootPath);
            foreach(var item in items)
            {
                Console.WriteLine(item.Name + "," + item.LocalPath + "," + item.Repo);
                var projectFiles = helper.GetAllProjectFiles(rootPath, item);
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
            var helper = new SolutionsHelper();
            var items = helper.LoadSolutions(rootPath);

            _buildUtils.Restore(rootPath, items.First());
        }

        [TestMethod]
        public void RemoveOldPackages()
        {
            _nugetUtils.RemoveAllOldPackages(rootPath);
        }

        [TestMethod]
        public void UpdateNugetVersions()
        {
            var helper = new SolutionsHelper();
            var items = helper.LoadSolutions(rootPath);
            var nugetVersion = _nugetHelpers.GenerateNugetVersion(1, 3, DateTime.Now);

            var projectFiles = _solutionHelper.GetAllProjectFiles(rootPath, items.First());
            foreach(var projectFile in projectFiles)
            {
                _nugetHelpers.ApplyToCSProject(projectFile, nugetVersion);
            }

            var nuspecFiles = _nugetHelpers.GetAllNuspecFiles(rootPath, items.First());
            foreach(var specFile in nuspecFiles)
            {
                _nugetHelpers.ApplyToNuspecFile(specFile, nugetVersion);
            }
        }

        [TestMethod]
        public void BuildSolution()
        {
            var helper = new SolutionsHelper();
            var items = helper.LoadSolutions(rootPath);

            _buildUtils.Build(rootPath, items.First(), "release");
        }

        [TestMethod]
        public void CreatePackages()
        {
            var helper = new SolutionsHelper();
            var items = helper.LoadSolutions(rootPath);

            _nugetUtils.CreatePackage(rootPath, items.First());
        }
    }
}
