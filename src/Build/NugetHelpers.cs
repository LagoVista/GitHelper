using LagoVista.Core.Validation;
using LagoVista.GitHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitHelper.Build
{
    public class NugetHelpers
    {
        const string NUGETVERSION1 = @"<PackageReference\s+Include\s*=\s*""LagoVista.(?'assembly'[\w\.]+)""\s+Version\s*=\s*""(?'version'[\w\.-]*)""\s+\/>";
        const string NUSPECVERSOIN_REGEX = @"<version>\s*(?'version'[\w\.-]+)\s*<\/version>";


        IFileHelper _fileHelper;
        SolutionHelper _solutionsHelper;
        IConsoleWriter _consoleWriter;


        public NugetHelpers(IConsoleWriter consoleWriter, IFileHelper fileHelper, SolutionHelper solutionHelper)
        {
            _consoleWriter = consoleWriter;
            _fileHelper = fileHelper;
            _solutionsHelper = solutionHelper;
        }

        public string GenerateNugetVersion(int major, int minor, DateTime dateStamp)
        {
            var days = Convert.ToInt32((dateStamp - new DateTime(2017, 5, 17)).TotalDays);
            var timeStamp = $"{dateStamp.Hour.ToString("00")}{dateStamp.Minute.ToString("00")}";

            return $"{major}.{minor}.{days}-beta{timeStamp}";
        }

        public InvokeResult SaveBackup(string fileName)
        {
            try
            {
                var nugetRegEx = new Regex(NUGETVERSION1);

                var fileOpenResult = _fileHelper.OpenFile(fileName);
                if (!fileOpenResult.Successful) return fileOpenResult.ToInvokeResult();

                var fileContents = fileOpenResult.Result;
                var matches = nugetRegEx.Matches(fileContents);
                foreach (Match match in matches)
                {
                    Console.WriteLine("LagoVista." + match.Groups["assembly"].Value + "=" + match.Groups["version"].Value);
                }
            }
            catch(Exception ex)
            {
                _consoleWriter.AddMessage(LogType.Error, $"Exception: {ex.Message} - Save Nuget Backup File: {fileName} ");
                return InvokeResult.FromException("NugetHelpers_SaveBackup", ex);
            }

            return InvokeResult.Success;
        }

        public InvokeResult ApplyToCSProjects(string rootPath, SolutionInformation solution, string nugetVersion)
        {
            try
            {
                var csProjs = _solutionsHelper.GetAllProjectFiles(rootPath, solution);
                foreach (var csProj in csProjs)
                {
                    var result = ApplyToCSProject(csProj, nugetVersion);
                    if (!result.Successful) return result;
                }
            }
            catch(Exception ex)
            {
                _consoleWriter.AddMessage(LogType.Error, $"Exception: {ex.Message} - Apply Nuget Version to Solution: {solution.Name} ");
                return InvokeResult.FromException("NugetHelpers_ApplyToCSProjects", ex);
            }

            return InvokeResult.Success;
        }

        public InvokeResult ApplyToAllNuspecFiles(string rootPath, SolutionInformation solution, string nugetVersion)
        {
            try
            {
                var files = GetAllNuspecFiles(rootPath, solution);
                foreach (var file in files)
                {
                    var result = ApplyToNuspecFile(file, nugetVersion);
                    if (!result.Successful) return result;
                }
            }
            catch(Exception ex)
            {
                _consoleWriter.AddMessage(LogType.Error, $"Exception: {ex.Message} - Apply Nuget Version to All Nuspec Files: {solution.Name} ");
                return InvokeResult.FromException("NugetHelpers_ApplyToAllNuspecFiles", ex);
            }


            return InvokeResult.Success;
        }

        public InvokeResult ApplyToCSProject(string fileName, string nugetVersion)
        {
            try
            {
                var nugetRegEx = new Regex(NUGETVERSION1);
                var result = _fileHelper.OpenFile(fileName);
                if (!result.Successful) return result.ToInvokeResult();

                var fileContents = result.Result;
                var matches = nugetRegEx.Matches(fileContents);

                var replace = @"<PackageReference Include=""LagoVista.${assembly}"" Version=""" + nugetVersion + @""" />";
                var newFileContent = nugetRegEx.Replace(fileContents, replace);

                _fileHelper.WriteFile(fileName, newFileContent);
            }
            catch(Exception ex)
            {
                _consoleWriter.AddMessage(LogType.Error, $"Exception: {ex.Message} - Apply Nuget Version to Project: {fileName} ");
                return InvokeResult.FromException("NugetHelpers_ApplyToCSProject", ex);
            }

            return InvokeResult.Success;
        }

        public InvokeResult ApplyToNuspecFile(string fileName, string nugetVersion)
        {

            try
            {
                var nugetRegEx = new Regex(NUSPECVERSOIN_REGEX);
                var result = _fileHelper.OpenFile(fileName);
                if (!result.Successful) return result.ToInvokeResult();

                var fileContents = result.Result;
                var matches = nugetRegEx.Matches(fileContents);

                var replace = $"<version>{nugetVersion}</version>";
                var newFileContent = nugetRegEx.Replace(fileContents, replace);
                _fileHelper.WriteFile(fileName, newFileContent);
            }
            catch(Exception ex)
            {
                _consoleWriter.AddMessage(LogType.Error, $"Exception: {ex.Message} - Apply Nuget Version to Nuspec File: {fileName} ");
                return InvokeResult.FromException("NugetHelpers_ApplyToNuspecFile", ex);
            }

            return InvokeResult.Success;

        }

        public List<string> GetAllNuspecFiles(string path, SolutionInformation solution)
        {
            var rootPath = Path.Combine(path, solution.LocalPath);
            var files = Directory.GetFiles(rootPath, "*.nuspec", SearchOption.AllDirectories);
            return files.ToList();
        }
    }
}
