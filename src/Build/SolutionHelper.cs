using LagoVista.Core.Validation;
using LagoVista.GitHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelper.Build
{
    public class SolutionHelper
    {
        IConsoleWriter _consoleWriter;
        IFileHelper _fileHelper;

        public SolutionHelper(IFileHelper fileHelper, IConsoleWriter consoleWriter)
        {
            _fileHelper = fileHelper;
            _consoleWriter = consoleWriter;
        }

        public InvokeResult<ObservableCollection<SolutionInformation>> LoadSolutions(string rootPath)
        {
            var solutionsFile = Path.Combine(rootPath, "Solutions.json");

            if (!System.IO.File.Exists(solutionsFile))
            {
                _consoleWriter.AddMessage(LogType.Error, "Could not find solutions file in root.");
                return InvokeResult<ObservableCollection<SolutionInformation>>.FromError("Could not find  solutions file in root.");
            }

            try
            {
                var result = _fileHelper.OpenFile(solutionsFile);

                var items = JsonConvert.DeserializeObject<ObservableCollection<SolutionInformation>>(result.Result);

                return InvokeResult<ObservableCollection<SolutionInformation>>.Create(items);
            }
            catch (Exception ex)
            {
                _consoleWriter.AddMessage(LogType.Error, "Could not load solutions: " + ex.Message);
                return InvokeResult<ObservableCollection<SolutionInformation>>.FromException("SolutionHelper_LoadSolutions", ex);
            }
        }

        public List<string> GetAllProjectFiles(string path, SolutionInformation solution)
        {
            var rootPath = Path.Combine(path, solution.LocalPath);
            var files = Directory.GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories);
            return files.ToList();
        }
    }
}
