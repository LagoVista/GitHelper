using GitHelper.Build;
using LagoVista.Core.Commanding;
using LagoVista.GitHelper.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LagoVista.GitHelper.UnitTesting
{
    public class UnitTestingViewModel : INotifyPropertyChanged
    {
        Dispatcher _dispatcher;
        string _rootPath;
        IConsoleWriter _consoleWriter;

        Regex _resultsRegEx = new Regex(@"Total tests: (?'total'\d+). Passed: (?'passed'\d+). Failed: (?'failed'\d+). Skipped: (?'skipped'\d+).");

        private const string CONSOLE_TEST_RUNNER = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe";

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyChanged(string propertyName)
        {
            if (_dispatcher != null)
            {

                _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }
        }

        public UnitTestingViewModel(string rootPath, Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _rootPath = rootPath;

            _consoleWriter = new ConsoleWriter(UnitTestingActivityLog, dispatcher);
            RunAllCommand = new RelayCommand(RunAllTest, CanRunAll);
            RunSelectedTestCommand = new RelayCommand(RunSelected, CanRunSelected);
            var fullFileSpec = System.IO.Path.Combine(rootPath, "UnitTests.txt");
            if (System.IO.File.Exists(fullFileSpec))
            {
                var lines = System.IO.File.ReadAllLines(fullFileSpec);
                foreach (var file in lines)
                {
                    UnitTests.Add(new UnitTest(file));
                }
            }
            else
            {
                _consoleWriter.AddMessage(LogType.Error, $"Could not find UnitTest.txt at {rootPath}");
                _consoleWriter.Flush();
            }

            if (!System.IO.File.Exists(CONSOLE_TEST_RUNNER))
            {
                _consoleWriter.AddMessage(LogType.Error, $"Could not find test runner");
                _consoleWriter.AddMessage(LogType.Error, CONSOLE_TEST_RUNNER);
                _consoleWriter.AddMessage(LogType.Error, "Should be installed as part of VS. NET, potential enchancement would be to make this configurable.");
                _consoleWriter.Flush();
            }

        }

        public bool CanRunSelected(Object obj)
        {
            return SelectedUnitTest != null;
        }


        public bool CanRunAll(Object obj)
        {
            return true;
        }

        public async void RunAllTest(Object job)
        {
            foreach (var test in UnitTests)
            {
                await Task.Run(() =>
                {
                    RunProcess(CONSOLE_TEST_RUNNER, _rootPath, test);
                });
            }
        }

        public void RunSelected(Object obj)
        {
            Task.Run(() =>
            {
                RunProcess(CONSOLE_TEST_RUNNER, _rootPath, SelectedUnitTest);
            });
        }

        private void RunProcess(string cmd, string path, UnitTest test)
        {
            _consoleWriter.Flush(true);

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = test.FullPath,
                    UseShellExecute = false,
                    WorkingDirectory = path,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _consoleWriter.AddMessage(LogType.Message, $"cd {path}");
            _consoleWriter.AddMessage(LogType.Message, $"{proc.StartInfo.FileName} {proc.StartInfo.Arguments}");

            proc.Start();

            while (!proc.StandardOutput.EndOfStream)
            {
                var line = proc.StandardOutput.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Message, line);
                _consoleWriter.Flush();

                var result = _resultsRegEx.Match(line);
                if(result.Success)
                {
                    test.Total = Convert.ToInt32(result.Groups["total"].Value);
                    test.Passed = Convert.ToInt32(result.Groups["passed"].Value);
                    test.Failed = Convert.ToInt32(result.Groups["failed"].Value);
                    test.Skipped = Convert.ToInt32(result.Groups["skipped"].Value);
                }
                Console.WriteLine(line);
            }

            while (!proc.StandardError.EndOfStream)
            {
                var line = proc.StandardError.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Error, line);
                Console.WriteLine(line);
            }

            if (proc.ExitCode == 0)
            {
                _consoleWriter.AddMessage(LogType.Success, $"Completed running test");
            }
            else
            {
                _consoleWriter.AddMessage(LogType.Error, $"Error running tests!");
            }

            _consoleWriter.AddMessage(LogType.Message, "------------------------------");
            _consoleWriter.AddMessage(LogType.Message, "");
            _consoleWriter.Flush();
        }


        UnitTest _selectedUnitTest;
        public UnitTest SelectedUnitTest
        {
            get { return _selectedUnitTest; }
            set
            {
                _selectedUnitTest = value;
                NotifyChanged(nameof(SelectedUnitTest));
                RunSelectedTestCommand.RaiseCanExecuteChanged();
            }
        }


        public ObservableCollection<UnitTest> UnitTests { get; private set; } = new ObservableCollection<UnitTest>();

        public ObservableCollection<ConsoleOutput> UnitTestingActivityLog { get; private set; } = new ObservableCollection<ConsoleOutput>();
        public RelayCommand RunAllCommand { get; private set; }

        public RelayCommand RunSelectedTestCommand { get; private set; }

    }
}
