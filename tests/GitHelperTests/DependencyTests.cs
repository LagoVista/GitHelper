using GitHelper.Build;
using LagoVista.GitHelper.Dependencies;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelperTests
{
    [TestClass]
    public class DependencyTests
    {

        const string rootPath = @"D:\NuvIoT";

        NugetUtils _nugetUtils;
        NugetHelpers _nugetHelpers;
        SolutionHelper _solutionHelper;
        FileHelpers _fileHelpers;
        DependencyManager _dependencyManager;


        [TestInitialize]
        public void Init()
        {
            var consoleWriter = new ConsoleWriter();

            _fileHelpers = new FileHelpers(consoleWriter);
            

            _solutionHelper = new SolutionHelper(_fileHelpers, consoleWriter);
            var items = _solutionHelper.LoadSolutions(rootPath);


            _dependencyManager = new DependencyManager(rootPath, consoleWriter, null);


            //_nugetHelpers = new NugetHelpers(consoleWriter, new FileHelpers(consoleWriter), _solutionHelper);
        //    _nugetUtils = new NugetUtils(consoleWriter, new FileHelpers(consoleWriter), _nugetHelpers);
        }

        [TestMethod]
        public async Task GetCSProjs()
        {
            await _dependencyManager.PopulateDependencyTreeAsync();
        }

    }
}
