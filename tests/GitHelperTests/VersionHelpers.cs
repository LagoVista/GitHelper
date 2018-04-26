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
    public class VersionHelpers
    {
        [TestMethod]
        public void GenerateVersionTest()
        {
            var fileHelper = new Moq.Mock<IFileHelper>();

            var helper = new NugetHelpers(new ConsoleWriter(), fileHelper.Object, new SolutionHelper(fileHelper.Object, new ConsoleWriter()));
            Console.WriteLine(helper.GenerateNugetVersion(1, 1, DateTime.Now.AddHours(-3)));

        }
    }
}
