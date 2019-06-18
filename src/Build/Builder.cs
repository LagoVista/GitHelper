using LagoVista.Core.Commanding;
using LagoVista.Core.Validation;
using LagoVista.GitHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace GitHelper.Build
{
    public class Builder : INotifyPropertyChanged
    {
        private string _rootPath;
        private IConsoleWriter _writer;

        private SolutionHelper _solutionHelper;
        private NugetHelpers _nugetHelpers;
        private FileHelpers _fileHelper;

        private BuildUtils _buildUtils;
        private NugetUtils _nugetUtils;

        private readonly MainViewModel _mainViewModel;

        Dispatcher _dispatcher;        

        private bool _isCancelled;

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyChanged(string propertyName)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        public Builder(string rootPath, IConsoleWriter writer, Dispatcher dispatcher, MainViewModel mainVM)
        {
            _dispatcher = dispatcher;
            _rootPath = rootPath;
            _mainViewModel = mainVM;

            _writer = writer;
            _fileHelper = new FileHelpers(_writer);
            _solutionHelper = new SolutionHelper(_fileHelper, _writer);

            _nugetHelpers = new NugetHelpers(_writer, _fileHelper, _solutionHelper);

            _buildUtils = new BuildUtils(_writer);
            _nugetUtils = new NugetUtils(_writer, _fileHelper, _nugetHelpers);

            if (!System.IO.Directory.Exists(rootPath))
            {
                MessageBox.Show($"Root Directory does not exist: {_rootPath}");
                return;
            }

            var solutionsResult = _solutionHelper.LoadSolutions(_rootPath);
            if (!solutionsResult.Successful)
            {
                MessageBox.Show($"Could not find Solutions.json file in {_rootPath}");
            }
            else
            {
                SolutionFiles = solutionsResult.Result;
                foreach (var solution in SolutionFiles)
                {
                    solution.SetDispatcher(_dispatcher);
                    solution.Reset();
                    var result = _nugetHelpers.SetPackageNames(rootPath, solution);
                    if (!result.Successful)
                    {
                        MessageBox.Show(result.Errors.First().Message);
                    }
                }
            }

            BuildNowCommand = new RelayCommand(BuildNow, CanBuildNow);
            CancelBuildCommand = new RelayCommand(CancelBuild, CanCancelBuild);
        }


        ObservableCollection<SolutionInformation> _solutionFiles;

        public ObservableCollection<SolutionInformation> SolutionFiles
        {
            get { return _solutionFiles; }
            set
            {
                _solutionFiles = value;
                NotifyChanged(nameof(SolutionFiles));
            }
        }

        private string _nugetVersion;
        public string NugetVersion
        {
            get { return _nugetVersion; }
            set
            {
                _nugetVersion = value;
                NotifyChanged(nameof(NugetVersion));
            }
        }

        public void CancelBuild()
        {
            _isCancelled = true;
        }

        public bool CanCancelBuild()
        {
            return IsBusy;
        }

        public void BuildNow()
        {
            IsBusy = true;

            Task.Run(() =>
            {
                _mainViewModel.DisableFileWatcher();
                _writer.AddMessage(LogType.Message, "Starting build");
                _writer.Flush(true);
                var result = BuildAll("release", 2, 1);
                // since the nuspecs are hard coded to look for files in release folder building in debug causes problems
                //var result = BuildAll("debug", 2, 1);
                if (result.Successful)
                {
                    _writer.AddMessage(LogType.Success, "Build Succeeded");
                }
                else
                {
                    _writer.AddMessage(LogType.Error, "Build Failed!");
                }
                _writer.Flush(false);
                this._mainViewModel.EnableFileWatcher();

                _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                {
                    IsBusy = false;
                });
            });
        }

        public bool CanBuildNow()
        {
            return !IsBusy;
        }

        public InvokeResult BuildAll(string configuration, int major, int minor)
        {
            _isCancelled = false;
            if (SolutionFiles == null)
            {
                return InvokeResult.FromError("Build not configured, likely could not find Solutions.json in root directory.");
            }

            var processStart = DateTime.Now;

            var result = _nugetUtils.RemoveAllOldPackages(_rootPath);
            if (!result.Successful)
            {
                return result.ToInvokeResult();
            }

            NugetVersion = _nugetHelpers.GenerateNugetVersion(major, minor, DateTime.Now);

            var solutionsToBuild = SolutionFiles.Where(sol => sol.ShouldBuild && sol.Build);

            var updatedPackages = new List<string>();
            /* If we are doing a partial build, get a list of the packages names that will get built, if the list is empty when we
             * update the csproj files we will update everything to the latest nuget, otherwise we will only do what we are building */
            //if (PartialBuild)
            //{
            foreach (var solution in solutionsToBuild)
            {
                foreach (var packageName in solution.Packages)
                {
                    updatedPackages.Add(packageName);
                }
            }
            //}

            foreach (var solution in solutionsToBuild)
            {
                result = _nugetHelpers.ApplyToCSProjects(_rootPath, solution, NugetVersion, updatedPackages);
                if (!result.Successful)
                {
                    return result.ToInvokeResult();
                }

                result = _nugetHelpers.ApplyToAllNuspecFiles(_rootPath, solution, NugetVersion);
                if (!result.Successful)
                {
                    return result.ToInvokeResult();
                }
            }

            _writer.Flush(true);

            foreach (var solution in SolutionFiles)
            {
                solution.Reset();
            }

            var idx = 1;
            var totalCount = solutionsToBuild.Count();
            foreach (var solution in solutionsToBuild)
            {
                var start = DateTime.Now;
                _writer.AddMessage(LogType.Message, $"Build started: {solution.Name} ({idx++} of {totalCount})");
                _writer.AddMessage(LogType.Message, $"===============================================");
                solution.Status = BuildStatus.Restoring;
                solution.StatusMessage = "Restoring";
                result = _buildUtils.Restore(_rootPath, solution);
                if (!result.Successful)
                {
                    solution.Status = BuildStatus.Error;
                    solution.StatusMessage = result.Errors.First().Message;
                    return result.ToInvokeResult();
                }

                if (_isCancelled)
                {
                    _writer.AddMessage(LogType.Warning, $"Build Cancelled");
                    _writer.AddMessage(LogType.Success, $"");
                    _writer.Flush();

                    return InvokeResult.FromError("Build Cancelled");
                }

                solution.StatusMessage = "Building";
                solution.Status = BuildStatus.Building;
                result = _buildUtils.Build(_rootPath, solution, configuration);
                if (!result.Successful)
                {
                    solution.StatusMessage = result.Errors.First().Message;
                    solution.Status = BuildStatus.Error;
                    return result.ToInvokeResult();
                }

                if (_isCancelled)
                {
                    _writer.AddMessage(LogType.Warning, $"Build Cancelled");
                    _writer.AddMessage(LogType.Success, $"");
                    _writer.Flush();

                    return InvokeResult.FromError("Build Cancelled");
                }

                solution.Status = BuildStatus.Packaging;
                result = _nugetUtils.CreatePackage(_rootPath, solution);
                if (!result.Successful)
                {
                    solution.StatusMessage = result.Errors.First().Message;
                    solution.Status = BuildStatus.Error;
                    return result.ToInvokeResult();
                }

                if (_isCancelled)
                {
                    _writer.AddMessage(LogType.Warning, $"Build Cancelled");
                    _writer.AddMessage(LogType.Success, $"");
                    _writer.Flush();

                    return InvokeResult.FromError("Build Cancelled");
                }

                _writer.AddMessage(LogType.Success, $"Build Completed: {solution.Name} ({idx} of {totalCount})");
                var buildTime = DateTime.Now - start;
                var totalBuildTime = DateTime.Now - processStart;

                solution.StatusMessage = $"Built in {Math.Round(buildTime.TotalSeconds, 1)} seconds ";
                solution.Status = BuildStatus.Built;

                _writer.AddMessage(LogType.Success, $"Current Build: {Math.Round(buildTime.TotalSeconds, 1)} seconds");
                _writer.AddMessage(LogType.Success, $"Total        : {Math.Round(totalBuildTime.TotalSeconds, 1)} seconds");
                _writer.AddMessage(LogType.Success, $"");
                _writer.Flush(true);
            }

            _writer.AddMessage(LogType.Success, $"System Build Completed");
            _writer.AddMessage(LogType.Success, $"{Math.Round((DateTime.Now - processStart).TotalSeconds, 1)} seconds");
            _writer.AddMessage(LogType.Success, $"");
            _writer.Flush();

            return InvokeResult.Success;
        }

        public RelayCommand BuildNowCommand { get; private set; }
        public RelayCommand CancelBuildCommand { get; private set; }


        private bool _fullBuild = true;
        public bool FullBuild
        {
            get { return _fullBuild; }
            set
            {
                _fullBuild = value;
                NotifyChanged(nameof(FullBuild));
                if (PartialBuild && _fullBuild)
                {
                    PartialBuild = false;
                }

                if (value)
                {
                    foreach (var solution in SolutionFiles)
                    {
                        solution.Build = true;
                    }
                }
            }
        }

        private bool _partialBuild = false;
        public bool PartialBuild
        {
            get { return _partialBuild; }
            set
            {
                _partialBuild = value;
                NotifyChanged(nameof(PartialBuild));
                if (FullBuild && _partialBuild)
                {
                    FullBuild = false;
                }

                if (value)
                {
                    foreach (var solution in SolutionFiles)
                    {
                        solution.Build = false;
                    }
                }
            }
        }


        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                BuildNowCommand.RaiseCanExecuteChanged();
                CancelBuildCommand.RaiseCanExecuteChanged();
                NotifyChanged(nameof(IsBusy));
            }
        }


    }
}
