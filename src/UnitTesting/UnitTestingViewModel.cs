using GitHelper.Build;
using LagoVista.Core.Commanding;
using LagoVista.GitHelper.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LagoVista.GitHelper.UnitTesting
{
    public class UnitTestingViewModel
    {
        Dispatcher _dispatcher;
        string _rootPath;
        IConsoleWriter _consoleWriter;
        private FileHelpers _fileHelper;
        SolutionHelper _solutionHelper;
        NugetHelpers _nugetHelpers;

        public UnitTestingViewModel(string rootPath, Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _rootPath = rootPath;

            _consoleWriter = new ConsoleWriter(UnitTestingActivityLog, dispatcher);
            RunAllCommand = new RelayCommand(RunAllTest, CanRunAll);
            var fullFileSpec = System.IO.Path.Combine(rootPath, "UnitTests.txt");
            if (System.IO.File.Exists(fullFileSpec))
            {
                var lines = System.IO.File.ReadAllLines(fullFileSpec);
                foreach(var file in lines)
                {
                    UnitTests.Add(new UnitTest(file));
                }
            }
            else
            {
                _consoleWriter.AddMessage(LogType.Error, $"Could not find UnitTest.txt at {rootPath}");
                _consoleWriter.Flush();
            }
            
        }
       
        public bool CanRunAll(Object obj)
        {
            return true;
        }

        public void RunAllTest(Object job)
        {

        }


        public ObservableCollection<UnitTest> UnitTests { get; private set; } = new ObservableCollection<UnitTest>();

        public ObservableCollection<ConsoleOutput> UnitTestingActivityLog { get; private set; } = new ObservableCollection<ConsoleOutput>();
        public RelayCommand RunAllCommand { get; private set; }

    }
}
