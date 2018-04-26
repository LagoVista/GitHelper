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
        const string NUGETVERSION1 = @"<PackageReference\s+Include\s+=\s+""LagoVista.(?'assembly'[\w\.]+)""\s+Version=""(?'version'[\w\.-]+)""\s+\/>";
        const string NUSPECVERSOIN_REGEX = @"<version>\s*(?'version'[\w\.-]+)\s*<\/version>";

        IFileHelper _fileHelper;
        public NugetHelpers(IFileHelper fileHelper)
        {
            _fileHelper = fileHelper;
        }

        public string GenerateNugetVersion(int major, int minor, DateTime dateStamp)
        {
            var days = Convert.ToInt32((dateStamp - new DateTime(2017, 5, 17)).TotalDays);
            var timeStamp = $"{dateStamp.Hour.ToString("00")}{dateStamp.Minute.ToString("00")}";

            return $"{major}.{minor}.{days}-beta{timeStamp}";
        }

        public void SaveBackup(string fileName, string nugetVersion)
        {
            var nugetRegEx = new Regex(NUGETVERSION1);
            var fileContents = _fileHelper.OpenFile(fileName);
            var matches = nugetRegEx.Matches(fileContents);
            foreach (Match match in matches)
            {
                Console.WriteLine("LagoVista." + match.Groups["assembly"].Value + "=" + match.Groups["version"].Value);
            }
        }

        public void ApplyToCSProject(string fileName, string nugetVersion)
        {            
            var nugetRegEx = new Regex(NUGETVERSION1);
            var fileContents = _fileHelper.OpenFile(fileName);
            var matches = nugetRegEx.Matches(fileContents);

            var replace = @"<PackageReference Include = ""LagoVista.${assembly}"" Version=""" + nugetVersion + @""" />";
            var newFileContent = nugetRegEx.Replace(fileContents, replace);

            _fileHelper.WriteFile(fileName, newFileContent);
        }

        public void ApplyToNuspecFile(string fileName, string nugetVersion)
        {
            var nugetRegEx = new Regex(NUSPECVERSOIN_REGEX);
            var fileContents = _fileHelper.OpenFile(fileName);
            var matches = nugetRegEx.Matches(fileContents);

            var replace = $"<version>{nugetVersion}</version>";
            var newFileContent = nugetRegEx.Replace(fileContents, replace);
            _fileHelper.WriteFile(fileName, newFileContent);

        }

        public List<string> GetAllNuspecFiles(string path, SolutionInformation solution)
        {
            var rootPath = Path.Combine(path, solution.LocalPath);
            var files = Directory.GetFiles(rootPath, "*.nuspec", SearchOption.AllDirectories);
            return files.ToList();
        }
    }
}
