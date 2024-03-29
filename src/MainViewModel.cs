﻿using GitHelper.Build;
using LagoVista.Core.Commanding;
using LagoVista.GitHelper.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LagoVista.GitHelper
{
    public class MainViewModel : INotifyPropertyChanged
    {
        readonly Dispatcher _dispatcher;
        readonly ConsoleWriter _consoleWriter;
        readonly ConsoleWriter _buildConsoleWriter;
        readonly List<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();

        string _rootPath;
        readonly ViewSettings _veiwSettings = new ViewSettings();
        readonly Dependencies.DependencyManager _dependencyManager;

        public MainViewModel(Dispatcher dispatcher)
        {

            var rp = Properties.Settings.Default["RootPath"];
            RootPath = rp == null ? @"D:\NuvIoT" : rp.ToString();

            if (!System.IO.Directory.Exists(RootPath))
            {
                MessageBox.Show($"Path [{RootPath}] does not exist, please set it to the root of your project structure, save settings and restart the application.");
                IsReady = false;
                SaveRootPathCommand = new RelayCommand(SaveRootPath);
                return;
            }

            _dispatcher = dispatcher;
            _consoleWriter = new ConsoleWriter(ConsoleLogOutput, dispatcher);
            _buildConsoleWriter = new ConsoleWriter(BuildConsoleLogOutput, dispatcher);

            BuildTools = new Builder(_rootPath, _buildConsoleWriter, dispatcher, this);

            RefreshCommand = new RelayCommand(Refresh, CanRefresh);
            AddSelectedNotStagedCommand = new RelayCommand(AddSelectedNotStaged);

            SelectedAllNotStagedCommand = new RelayCommand(SelectAllNotStaged);
            ClearAllNotStagedCommand = new RelayCommand(ClearAllNotStaged);

            IsReady = true;
            _dependencyManager = new Dependencies.DependencyManager(_rootPath,  _dispatcher);
            UnitTestingViewModel = new UnitTesting.UnitTestingViewModel(_rootPath, _dispatcher);

        }

        public void EnableFileWatcher()
        {
            _fileWatchers.ForEach(watch => watch.EnableRaisingEvents = true);
        }

        public void DisableFileWatcher()
        {
            _fileWatchers.ForEach(watch => watch.EnableRaisingEvents = false);
        }

        public bool IsReady { get; private set; }


        public bool CanRefresh(Object obj)
        {
            return !IsBusy;
        }


        public void SaveRootPath()
        {
            Properties.Settings.Default["RootPath"] = RootPath;
            Properties.Settings.Default.Save();
            MessageBox.Show($"New Path [{RootPath}] is set, please restart the application to continue.");
        }

        public event PropertyChangedEventHandler PropertyChanged;


        private void NotifyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Refresh(Object obj)
        {
            ScanNow();
        }

        public void SelectAllNotStaged()
        {
            foreach (var file in CurrentFolder.NotStaged)
            {
                file.Selected = true;
            }
        }

        public void ClearAllNotStaged()
        {
            foreach (var file in CurrentFolder.NotStaged)
            {
                file.Selected = false;
            }
        }

        public void AddSelectedNotStaged()
        {
            var bldr = new List<String>();
            foreach(var file in CurrentFolder.NotStaged)
            {
                if(file.Selected)
                {
                    bldr.Add($"\"{file.FullPath}\"");
                }
            }

            IsBusy = true;
            Task.Run(() =>
            {
                CurrentFolder.RunProcess("git.exe", $"add {String.Join(" ", bldr)}", "adding file", checkRemote: false);
            });
        }

        public void ScanNow()
        {
            IsBusy = true;

            var dirs = System.IO.Directory.GetDirectories(_rootPath).Select(dir=>dir.ToLower()).ToList();

            dirs.Remove($"{_rootPath}\\localpacakges");
            dirs.Remove($"{_rootPath}\\localprivatepacakges");
            dirs.Remove($"{_rootPath}\\do.docs");
            dirs.Remove($"{_rootPath}\\do.documentation");
            dirs.Remove($"{_rootPath}\\examples");
            dirs.Remove($"{_rootPath}\\buildscripts");

            ScanMax = dirs.Count();
            ScanProgress = 0;
            ScanVisibility = Visibility.Visible;

            var idx = 0;
            var count = dirs.Count();
           
            Task.Run(() =>
            {
                IsBusy = true;

                var folders = new List<GitManagedFolder>();
                
                Parallel.ForEach(dirs, (dir) =>
                {
                    _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                    {
                        Status = $"Scanning: {dir} {idx++} of {count}.";
                        ScanProgress = ScanProgress++;
                    });

                    var folder = ScanTree(dir);
                    if (folder != null)
                    {
                        folders.Add(folder);
                        folder.IsBusyEvent += (sndr, args) => IsBusy = args;
                        var fileWatcher = new FileSystemWatcher(dir, "*")
                        {
                            IncludeSubdirectories = true,
                            EnableRaisingEvents = true,
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.DirectoryName | NotifyFilters.LastAccess
                        };
                        fileWatcher.Created += FileWatcher_Created;
                        fileWatcher.Deleted += FileWatcher_Deleted;
                        fileWatcher.Renamed += FileWatcher_Renamed;
                        fileWatcher.Changed += FileWatcher_Changed;
                        _fileWatchers.Add(fileWatcher);
                    }
                });


                _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                {
                    Folders = new ObservableCollection<GitManagedFolder>(folders.OrderBy(f => f.Label));
                });

                //foreach (var dir in dirs)
                //{
                //    _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                //    {
                //        Status = "Scanning: " + dir;
                //        ScanProgress = ScanProgress + 1.0;
                //    });

                //    var folder = ScanTree(dir);
                //    IsBusy = true;
                //    if (folder != null)
                //    {
                //        folder.IsBusyEvent += (sndr, args) => IsBusy = args;
                //        var fileWatcher = new FileSystemWatcher(dir, "*");
                //        fileWatcher.IncludeSubdirectories = true;
                //        fileWatcher.EnableRaisingEvents = true;
                //        fileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.DirectoryName | NotifyFilters.LastAccess;
                //        fileWatcher.Created += FileWatcher_Created;
                //        fileWatcher.Deleted += FileWatcher_Deleted;
                //        fileWatcher.Renamed += FileWatcher_Renamed;
                //        fileWatcher.Changed += FileWatcher_Changed;
                //        _fileWatchers.Add(fileWatcher);

                //        _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                //        {
                //            Folders.Add(folder);
                //        });
                //    }
                //}

                _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                {
                    Status = "Ready";
                    ScanVisibility = Visibility.Collapsed;
                    IsBusy = false;
                });
            });

        }

        private bool _isBusy = false;
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        NotifyChanged(nameof(IsBusy));
                        RefreshCommand.RaiseCanExecuteChanged();
                    });
                }
            }
        }

        #region File Watcher
        private readonly List<string> _ignoredFileTypes = new List<string>()
        {
            ".cache",
            ".tmp",
            ".dll",
            ".obj",
            ".suo",
            ".props",
            ".json",
            ".ide",
            ".git",
            ".lock",
            ".ide-wal",
        };

        private readonly Dictionary<string, DateTime> _updateTimeStamps = new Dictionary<string, DateTime>();

        private bool ShouldIgnore(string directoryName, string fullFileName, string changeType)
        {
            var extension = Path.GetExtension(fullFileName).ToLower();
            if (_ignoredFileTypes.Contains(extension))
            {
                return true;
            }

            /* Anything in the .git directory should be igmored */
            var localFileName = fullFileName.Substring(directoryName.Length + 1);
            if (localFileName.StartsWith(".git"))
            {
                return true;
            }

            lock (_updateTimeStamps)
            {
                if (_updateTimeStamps.Keys.Contains(fullFileName))
                {
                    var lastDateStamp = _updateTimeStamps[fullFileName];
                    if ((DateTime.Now - lastDateStamp) < TimeSpan.FromSeconds(1))
                    {
                        Console.WriteLine(@"File " + changeType + " " + fullFileName);
                        Console.WriteLine($"Ignored By File Update, {lastDateStamp} - {DateTime.Now} ");
                        Console.WriteLine("-------------------------");
                        return true;
                    }
                    else
                    {
                        _updateTimeStamps[fullFileName] = DateTime.Now;
                    }
                }
                else
                {
                    _updateTimeStamps.Add(fullFileName, DateTime.Now);
                }
            }

            Console.WriteLine(@"File " + " " + changeType + " " + fullFileName);
            Console.WriteLine("To be processed");
            Console.WriteLine("-------------------------");

            return false;
        }

        private void HandleFileUpdated(string directoryName, string fullFileName, string changeType)
        {
            /* If we are in a build or other operation, ignore any updates files, will do a scan afterwards */
            if (IsBusy)
            {
                return;
            }

            var extension = Path.GetExtension(fullFileName).ToLower();
            if (ShouldIgnore(directoryName, fullFileName, changeType))
            {
                return;
            }

            var folder = Folders.Where(fld => fld.Path == directoryName).FirstOrDefault();
            if (folder != null)
            {
                var added = false;

                var file = folder.NotStaged.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                if (file == null)
                {
                    file = folder.Untracked.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                }

                if (file == null)
                {
                    file = folder.Conflicted.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                }

                if (file == null)
                {
                    file = folder.Staged.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                }

                if (file == null)
                {
                    file = new GitManagedFile(this._dispatcher, _veiwSettings, folder)
                    {
                        Directory = directoryName,
                        FullPath = fullFileName,
                        Label = fullFileName.Substring(directoryName.Length + 1),
                        FileType = FileTypes.SourceFile,
                        State = GitFileState.NotStaged

                    };

                    added = true;
                }

                var isTracked = IsTracked(file);
                file.State = isTracked ? GitFileState.NotStaged : GitFileState.Untracked;
                var changes = String.Empty;
                if (isTracked)
                {
                    changes = DetectChanges(file);
                    if (String.IsNullOrEmpty(changes))
                    {
                        _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                        {
                            if (folder.NotStaged != null)
                            {
                                folder.NotStaged.Remove(file);
                                folder.NotStagedFileStatus = folder.NotStaged.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                            }
                        });

                        return;
                    }
                }
                else
                {
                    changes = DetectChanges(file);
                    file.IsDirty = true;
                }

                _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                {
                    file.Changes = changes;
                    file.Analyze();
                    if (added)
                    {
                        Console.WriteLine("Adding file: " + fullFileName);
                        switch (file.State)
                        {
                            case GitFileState.Untracked:
                                if (!folder.Untracked.Where(fil => fil.Label == file.Label).Any())
                                {
                                    folder.Untracked.Add(file);
                                }

                                break;
                            case GitFileState.Staged:
                                break;
                            case GitFileState.Conflicted:
                                break;
                            case GitFileState.NotStaged:
                                if (folder.NotStaged != null && !folder.NotStaged.Where(fil => fil.Label == file.Label).Any())
                                {
                                    folder.Untracked.Add(file);
                                }

                                break;
                        }
                    }

                    folder.NotStagedFileStatus = folder.NotStaged.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                    folder.UntrackedFileStatus = folder.Untracked.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                    folder.ConflictFileStatus = folder.Conflicted.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                    folder.IsDirty = folder.NotStagedFileStatus == CurrentStatus.Dirty || folder.ConflictFileStatus == CurrentStatus.Dirty || folder.UntrackedFileStatus == CurrentStatus.Dirty;
                });
            }
        }

        private void WasRemoved(string directoryName, string fullFileName)
        {
            if (Folders != null)
            {
                var folder = Folders.Where(fld => fld.Path == directoryName).FirstOrDefault();
                if (folder != null)
                {
                    _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                    {
                        if (folder.Untracked != null)
                        {
                            var file = folder.Untracked.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                            if (file != null)
                            {
                                folder.Untracked.Remove(file);
                            }
                            folder.UntrackedFileStatus = folder.Untracked.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                        }
                        folder.IsDirty = folder.NotStagedFileStatus == CurrentStatus.Dirty || folder.ConflictFileStatus == CurrentStatus.Dirty || folder.UntrackedFileStatus == CurrentStatus.Dirty;
                    });
                }
            }
        }

        private bool IsTracked(GitManagedFile status, bool diagnostics = false)
        {
            if (diagnostics)
            {
                Console.WriteLine(status.FullPath);
                Console.WriteLine(status.Label);
            }

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = $"status {status.Label}",
                    UseShellExecute = false,
                    WorkingDirectory = status.Directory,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.ErrorDataReceived += (sndr, args) =>
            {
                Console.WriteLine(args.Data);
            };

            var bldr = new StringBuilder();

            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine().Trim();
                if (diagnostics)
                {
                    Console.WriteLine(line);
                }

                if (line.StartsWith("Untracked files:"))
                {
                    Console.WriteLine("Found staged file");
                    return false;
                }
            }

            return true;
        }

        private string DetectChanges(GitManagedFile status, bool diagnostics = false)
        {
            if (diagnostics)
            {
                Console.WriteLine(status.FullPath);
                Console.WriteLine(status.Label);
            }

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = $"diff {status.Label}",
                    UseShellExecute = false,
                    WorkingDirectory = status.Directory,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            var bldr = new StringBuilder();

            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine().Trim();
                if (diagnostics)
                {
                    Console.WriteLine(line);
                }

                if ((line.StartsWith("-") || line.StartsWith("+")) &&
                    (!line.StartsWith("+++") && !line.StartsWith("---")))
                {
                    bldr.AppendLine(line);
                }
            }

            return bldr.ToString();
        }

        private bool IsGitManagedFile(string item)
        {
            if (item.EndsWith("TMP") || item.EndsWith("~"))
            {
                return false;
            }

            try
            {
                if (System.IO.File.Exists(item))
                {
                    return (File.GetAttributes(item) & FileAttributes.Directory) != FileAttributes.Directory;

                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }


        private void FileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (IsGitManagedFile(e.FullPath))
            {
                var fsw = sender as FileSystemWatcher;
                HandleFileUpdated(fsw.Path, e.FullPath, "Renamed");
            }
        }

        private void FileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (IsGitManagedFile(e.FullPath))
            {
                var fsw = sender as FileSystemWatcher;
                WasRemoved(fsw.Path, e.FullPath);
            }
        }

        private void FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (IsGitManagedFile(e.FullPath))
            {
                var fsw = sender as FileSystemWatcher;
                HandleFileUpdated(fsw.Path, e.FullPath, "Created");
            }
        }

        private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (IsGitManagedFile(e.FullPath))
            {
                var fsw = sender as FileSystemWatcher;
                HandleFileUpdated(fsw.Path, e.FullPath, "Changed");
            }
        }
        #endregion

        public GitManagedFolder ScanTree(String dir)
        {
            if (dir.ToLower().Contains("do.doc")
                || dir.ToLower().Contains("dockerbuild")
                || dir.ToLower().Contains("examples")
                || dir.ToLower().Contains("buildscripts")
                || dir.ToLower().Contains("localprivatepackages")
                || dir.ToLower().Contains("localpackages"))
            {
                return null;
            }

            Debug.WriteLine($"+Strarting: " + dir);

            var folder = new GitManagedFolder(_dispatcher, _veiwSettings, _consoleWriter)
            {
                Label = dir.Split('\\').Last(),
                Path = dir
            };
            var result = folder.Scan(resetAllClear: false);


            Debug.WriteLine($"-Completed: " + dir);

            return result ? folder : null;
        }

        #region Properties
        public Builder BuildTools { get; }


        ObservableCollection<GitManagedFolder> _folders;
        public ObservableCollection<GitManagedFolder> Folders
        {
            get { return _folders; }
            set
            {
                _folders = value;
                NotifyChanged(nameof(Folders));
            }
        }

        private double _scanProgress;
        public double ScanProgress
        {
            get { return _scanProgress; }
            set
            {
                _scanProgress = value;
                NotifyChanged(nameof(ScanProgress));
            }
        }

        private int _scanMax;
        public int ScanMax
        {
            get { return _scanMax; }
            set
            {
                _scanMax = value;
                NotifyChanged(nameof(ScanMax));
            }
        }

        private Visibility _scanVisibility;
        public Visibility ScanVisibility
        {
            get { return _scanVisibility; }
            set
            {
                _scanVisibility = value;
                NotifyChanged(nameof(ScanVisibility));
            }
        }

        public String RootPath
        {
            get { return _rootPath; }
            set
            {
                _rootPath = value;
                NotifyChanged(nameof(RootPath));
            }
        }


        private string _status;
        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                NotifyChanged(nameof(Status));
            }
        }

        public UnitTesting.UnitTestingViewModel UnitTestingViewModel { get; private set; }


        public Dependencies.DependencyManager DependencyManager { get { return _dependencyManager; } }

        public ObservableCollection<ConsoleOutput> ConsoleLogOutput { get; } = new ObservableCollection<ConsoleOutput>();

        public ObservableCollection<ConsoleOutput> BuildConsoleLogOutput { get; } = new ObservableCollection<ConsoleOutput>();

        GitManagedFile _currentFile = null;
        public GitManagedFile CurrentFile
        {
            get { return _currentFile; }
            set
            {
                _currentFile = value;
                NotifyChanged(nameof(CurrentFile));
            }
        }

        GitManagedFolder _currentFolder = null;
        public GitManagedFolder CurrentFolder

        {
            get { return _currentFolder; }
            set
            {
                _currentFolder = null;
                NotifyChanged(nameof(CurrentFolder));

                if (value != null)
                {
                    _dispatcher.BeginInvoke((Action)delegate
                    {
                        Status = "Scanning " + value.Label;
                    });

                    Task.Run(() =>
                    {
                        IsBusy = true;
                        value.Scan(true);

                        _dispatcher.BeginInvoke((Action)delegate
                       {
                           _currentFolder = value;
                           Status = "Ready " + _currentFolder.Label;
                           NotifyChanged(nameof(CurrentFolder));
                       });
                    });
                }


            }
        }
        #endregion

        #region Commands

        public RelayCommand RefreshCommand { get; private set; }
        public RelayCommand SaveRootPathCommand { get; private set; }

        public RelayCommand AddSelectedNotStagedCommand { get; private set; }
        public RelayCommand SelectedAllNotStagedCommand { get; private set; }
        public RelayCommand ClearAllNotStagedCommand { get; private set; }

        #endregion
    }
}
