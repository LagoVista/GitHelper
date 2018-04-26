using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelper.Build
{
    public class SolutionsHelper
    {
        public List<SolutionInformation> LoadSolutions(string rootPath)
        {
            var contents = System.IO.File.ReadAllText(Path.Combine(rootPath, "Solutions.json"));

            var items = JsonConvert.DeserializeObject<List<SolutionInformation>>(contents);

            return items;
        }

        public List<string> GetAllProjectFiles(string path, SolutionInformation solution)
        {
            var rootPath = Path.Combine(path, solution.LocalPath);
            var files = Directory.GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories);
            return files.ToList();
        }      
    }
}
