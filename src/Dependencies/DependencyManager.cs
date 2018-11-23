using GitHelper.Build;
using LagoVista.Core.Commanding;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LagoVista.GitHelper.Dependencies
{
    public class DependencyManager : INotifyPropertyChanged
    {
        Dispatcher _dispatcher;
        string _rootPath;
        IConsoleWriter _consoleWriter;
        private FileHelpers _fileHelper;
        SolutionHelper _solutionHelper;
        NugetHelpers _nugetHelpers;

        public DependencyManager(string rootPath, Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _rootPath = rootPath;

            _consoleWriter = new ConsoleWriter(GitCommitLog, dispatcher);
            _fileHelper = new FileHelpers(_consoleWriter);
            _solutionHelper = new SolutionHelper(_fileHelper, _consoleWriter);

            _nugetHelpers = new NugetHelpers(_consoleWriter, _fileHelper, _solutionHelper);

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
                }
            }

            RefreshCommand = new RelayCommand(async (obj) => await PopulateDependencyTreeAsync(obj), CanRefresh);
            UpdateNugetVersionCommand = new RelayCommand(UpdateVersion, CanUpdateNuget);
        }

        public bool CanUpdateNuget(Object obj)
        {
            return !IsUpdatingNuget;
        }

        public bool CanRefresh(Object obj)
        {
            return !IsLoading;
        }


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

        #region Populate Tree
        public async Task PopulateDependencyTreeAsync(Object obj)
        {
            Packages.Clear();
            NotifyChanged(nameof(Packages));

            IsLoading = true;
            
            SelectedPackage = null;
            SelectedProject = null;

            var packages = new ObservableCollection<Models.Package>();
            ActivityLog.Clear();

            var client = new HttpClient();

            foreach (var solution in SolutionFiles)
            {
                var csProjs = _solutionHelper.GetAllProjectFiles(_rootPath, solution);

                foreach (var proj in csProjs)
                {
                    var packageResult = _nugetHelpers.GetAllPackages(proj);
                    if (packageResult.Successful)
                    {
                        foreach (var package in packageResult.Result)
                        {
                            var existingPackage = packages.Where(pkg => pkg.Name == package.Name).FirstOrDefault();
                            if (existingPackage != null)
                            {
                                var existingVersion = existingPackage.InstalledVersions.Where(ver => ver.Version == package.InstalledVersions.FirstOrDefault().Version).FirstOrDefault();
                                if (existingVersion != null)
                                {
                                    existingVersion.ProjectFiles.Add(new Models.ProjectFile()
                                    {
                                        FullPath = proj,
                                        Version = existingVersion.Version                                        
                                    });
                                }
                                else
                                {
                                    existingPackage.AddInstalledVersion(package.InstalledVersions.First());
                                }
                            }
                            else
                            {
                                ActivityLog.Insert(0, package.Name);
                                if (!package.Name.StartsWith("LagoVista"))
                                {
                                    var json = await client.GetStringAsync($"https://api-v2v3search-0.nuget.org/query?q={package.Name}&prerelease=false");
                                    var result = JsonConvert.DeserializeObject<Models.NugetResult>(json);
                                    var serverPackage = result.Pacakges.Where(pkg => pkg.Id == package.Name).FirstOrDefault();
                                    if (serverPackage != null)
                                    {
                                        package.Current = serverPackage.Version;
                                    }
                                    else
                                    {
                                        package.Current = "??";
                                    }

                                    json = await client.GetStringAsync($"https://api-v2v3search-0.nuget.org/query?q={package.Name}&prerelease=true&t={DateTime.Now.Millisecond}`");
                                    result = JsonConvert.DeserializeObject<Models.NugetResult>(json);
                                    serverPackage = result.Pacakges.Where(pkg => pkg.Id == package.Name).FirstOrDefault();
                                    if (serverPackage != null)
                                    {
                                        package.Prerelease = serverPackage.Version;
                                        foreach (var version in serverPackage.Versions)
                                        {
                                            package.AddVersion(new Models.PackageVersion()
                                            {
                                                 Version = version.Version
                                            });
                                        }
                                    }
                                    else
                                    {
                                        package.Prerelease = "??";
                                    }

                                }
                                else
                                {
                                    package.Current = "-";
                                }

                                packages.Add(package);
                            }
                        }
                    }
                }
            }

            Packages = new ObservableCollection<Models.Package>(packages.OrderBy(pkg => pkg.Name));
            IsLoading = false;
        }
        #endregion

        public void UpdateVersion(Object obj)
        {
            

            if(SelectedProject != null && SelectedPackage != null)
            {
                IsUpdatingNuget = true;
                var oldVersion = SelectedProject.Version;

                _consoleWriter.Flush(true);
                _consoleWriter.AddMessage(LogType.Message, $"Updating {SelectedPackage.Name} on {SelectedProject.Name} to {SelectedPackage.SelectedVersion.Version}");
                _consoleWriter.Flush();

                var result = _nugetHelpers.ApplyToCSProject(SelectedProject.FullPath, SelectedPackage.SelectedVersion.Version, SelectedPackage.Name );
                if (result.Successful)
                {
                    _consoleWriter.AddMessage(LogType.Message, $"Success Updating {SelectedPackage.Name} on {SelectedProject.Name} to {SelectedPackage.SelectedVersion.Version}");
                    _consoleWriter.Flush();
                    var path = SelectedProject.Path;
                    var commitMessage = result.Result;

                    if (AutoCommit)
                    {
                        Task.Run(() =>
                        {
                            RunProcess("git.exe", path, $"add .", "adding files", checkRemote: false);
                            RunProcess("git.exe", path, $"commit -m \"{commitMessage}\"", "committing files", checkRemote: false);
                            RunProcess("git.exe", path, $"push", "Pushing Files", checkRemote: false);
                            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                            {
                                IsUpdatingNuget = false;
                                var oldPackageVersion = SelectedPackage.InstalledVersions.Where(ver => ver.Version == oldVersion).FirstOrDefault();
                                if (oldPackageVersion != null)
                                {
                                    oldPackageVersion.ProjectFiles.Remove(SelectedProject);
                                }

                                if (!oldPackageVersion.ProjectFiles.Any())
                                {
                                    SelectedPackage.InstalledVersions.Remove(oldPackageVersion);
                                }

                                var newPackageVersion = SelectedPackage.InstalledVersions.Where(ver => ver.Version == SelectedPackage.SelectedVersion.Version).FirstOrDefault();
                                if (newPackageVersion != null)
                                {
                                    newPackageVersion.ProjectFiles.Add(SelectedProject);
                                }

                                NotifyChanged(nameof(SelectedPackage));
                                NotifyChanged(nameof(Packages));
                            });
                        });
                    }
                    else
                    {

                        var oldPackageVersion = SelectedPackage.InstalledVersions.Where(ver => ver.Version == oldVersion).FirstOrDefault();
                        if (oldPackageVersion != null)
                        {
                            oldPackageVersion.ProjectFiles.Remove(SelectedProject);
                        }

                        if (!oldPackageVersion.ProjectFiles.Any())
                        {
                            SelectedPackage.InstalledVersions.Remove(oldPackageVersion);
                        }

                        var newPackageVersion = SelectedPackage.InstalledVersions.Where(ver => ver.Version == SelectedPackage.SelectedVersion.Version).FirstOrDefault();
                        if (newPackageVersion != null)
                        {
                            newPackageVersion.ProjectFiles.Add(SelectedProject);
                        }

                        NotifyChanged(nameof(SelectedPackage));
                        NotifyChanged(nameof(Packages));
                    }

                }
                else
                {
                    _consoleWriter.AddMessage(LogType.Error, $"Failed Updating {SelectedPackage.Name} on {SelectedProject.Name} to {SelectedPackage.SelectedVersion.Version}");
                    _consoleWriter.AddMessage(LogType.Error,result.Errors.First().Message);
                    _consoleWriter.Flush();
                    IsUpdatingNuget = false;
                }                
            }
        }

        private void RunProcess(string cmd, string path, string args, string actionType, bool clearConsole = true, bool checkRemote = true)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = args,
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
            }

            while (!proc.StandardError.EndOfStream)
            {
                var line = proc.StandardError.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Error, line);
            }

            if (proc.ExitCode == 0)
            {
                _consoleWriter.AddMessage(LogType.Success, $"Success {actionType}");
            }
            else
            {
                _consoleWriter.AddMessage(LogType.Error, $"Error {actionType}!");
            }

            _consoleWriter.AddMessage(LogType.Message, "------------------------------");
            _consoleWriter.AddMessage(LogType.Message, "");
            _consoleWriter.Flush();
        }


        #region Properties
        
        private bool _autoCommit = true;
        public bool AutoCommit
        {
            get { return _autoCommit; }
            set
            {
                _autoCommit = value;
                NotifyChanged(nameof(AutoCommit));
            }
        }

        Models.Package _selectedPackage;
        public Models.Package SelectedPackage
        {
            get
            {
                return _selectedPackage;
            }
            set
            {
                _selectedPackage = value;
                NotifyChanged(nameof(SelectedPackage));
                SelectedProject = null;
            }
        }


        private bool _showAll = true;
        public bool ShowAll
        {
            get { return _showAll; }
            set
            {
                _showAll = value;
                NotifyChanged(nameof(ShowAll));
                if (ShowVersionMisMatch && _showAll)
                {
                    ShowVersionMisMatch = false;
                }

                if (ShowUpdatable && _showAll)
                {
                    ShowUpdatable = false;
                }

                NotifyChanged(nameof(Packages));
            }
        }

        private bool _showVersionMismatch = false;
        public bool ShowVersionMisMatch
        {
            get { return _showVersionMismatch; }
            set
            {
                _showVersionMismatch = value;
                NotifyChanged(nameof(ShowVersionMisMatch));
                if (ShowAll && _showVersionMismatch)
                {
                    ShowAll = false;
                }

                if (ShowUpdatable && _showVersionMismatch)
                {
                    ShowUpdatable = false;
                }

                NotifyChanged(nameof(Packages));
            }
        }

        private bool _showUpdatable = false;
        public bool ShowUpdatable
        {
            get { return _showUpdatable; }
            set
            {
                _showUpdatable = value;
                NotifyChanged(nameof(ShowUpdatable));
                if (ShowVersionMisMatch && _showUpdatable)
                {
                    ShowVersionMisMatch = false;
                }

                if (ShowAll && _showUpdatable)
                {
                    ShowAll = false;
                }

                NotifyChanged(nameof(Packages));
            }
        }

        private bool _showLagoVista = false;
        public bool ShowLagoVista
        {
            get { return _showLagoVista; }
            set
            {
                _showLagoVista = value;
                NotifyChanged(nameof(ShowLagoVista));
                NotifyChanged(nameof(Packages));
            }
        }

        private bool _prerelease = false;
        public bool Prerelease
        {
            get { return _prerelease; }
            set
            {
                _prerelease = value;
                NotifyChanged(nameof(Prerelease));
                foreach (var package in Packages)
                {
                    package.AllowPrelease = value;
                }
                NotifyChanged(nameof(Packages));
            }
        }


        private bool _isLoading = false;
        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                NotifyChanged(nameof(IsLoading));
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isUpdatingNuget = false;
        public bool IsUpdatingNuget
        {
            get { return _isUpdatingNuget; }
            set
            {
                _isUpdatingNuget = value;
                NotifyChanged(nameof(IsUpdatingNuget));
                UpdateNugetVersionCommand.RaiseCanExecuteChanged();
            }
        }

        Models.ProjectFile _selectedProject;
        public Models.ProjectFile SelectedProject
        {
            get { return _selectedProject; }
            set
            {
                _selectedProject = value;
                NotifyChanged(nameof(SelectedProject));
                if (SelectedPackage != null && value != null)
                {
                    SelectedPackage.SelectedVersion = SelectedPackage.AllVersions.Where(ver => ver.Version == value.Version).FirstOrDefault();
                }
            }
        }

        ObservableCollection<Models.Package> _packages = new ObservableCollection<Models.Package>();
        public ObservableCollection<Models.Package> Packages
        {
            get
            {
                if (ShowLagoVista)
                {
                    if (ShowVersionMisMatch)
                    {
                        return new ObservableCollection<Models.Package>(_packages.Where(pkg => pkg.VersionCount > 1));
                    }
                    else if (ShowUpdatable)
                    {
                        return new ObservableCollection<Models.Package>(_packages.Where(pkg => pkg.CanUpgarde));
                    }

                    return _packages;
                }
                else
                {
                    if (ShowVersionMisMatch)
                    {
                        return new ObservableCollection<Models.Package>(_packages.Where(pkg => pkg.VersionCount > 1 && !pkg.Name.StartsWith("LagoVista")));
                    }
                    else if (ShowUpdatable)
                    {
                        return new ObservableCollection<Models.Package>(_packages.Where(pkg => pkg.CanUpgarde && !pkg.Name.StartsWith("LagoVista")));
                    }

                    return new ObservableCollection<Models.Package>(_packages.Where(pkg => !pkg.Name.StartsWith("LagoVista")));
                }
            }
            set
            {
                _packages = value;
                NotifyChanged(nameof(Packages));
            }
        }

        public ObservableCollection<string> ActivityLog { get; private set; } = new ObservableCollection<string>();
        public ObservableCollection<ConsoleOutput> GitCommitLog { get; private set; } = new ObservableCollection<ConsoleOutput>();

        public RelayCommand RefreshCommand { get; private set; }
        public RelayCommand UpdateNugetVersionCommand { get; private set; }
        #endregion
    }
}
