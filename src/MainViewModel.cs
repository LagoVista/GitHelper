using GitHelper.Build;
using LagoVista.Core.Commanding;
using LagoVista.GitHelper;
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
using System.Windows.Input;
using System.Windows.Threading;

namespace LagoVista.GitHelper
{
    public class MainViewModel : INotifyPropertyChanged
    {
        Dispatcher _dispatcher;
        private ObservableCollection<ConsoleOutput> _consoleOutput = new ObservableCollection<ConsoleOutput>();
        ConsoleWriter _consoleWriter;

        Builder _builder;
        String _rootPath;

        public MainViewModel(Dispatcher dispatcher, string rootPath)
        {
            _rootPath = rootPath;
            _dispatcher = dispatcher;
            _consoleWriter = new ConsoleWriter(_consoleOutput, dispatcher);

            _builder = new Builder(rootPath, _consoleWriter);

            BuildNowCommand = new RelayCommand(BuildNow);
        }


        public event PropertyChangedEventHandler PropertyChanged;
        List<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();


        private void NotifyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void ScanNow()
        {
            var dirs = System.IO.Directory.GetDirectories(_rootPath);
            ScanMax = dirs.Length;
            ScanVisibility = Visibility.Visible;

            Task.Run(() =>
            {
                Folders = new ObservableCollection<GitManagedFolder>();

                foreach (var dir in dirs.Take(3))
                {
                    _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                    {
                        Status = "Scanning: " + dir;
                        ScanProgress = ScanProgress + 1.0;
                    });


                    var folder = ScanTree(dir);
                    if (folder != null)
                    {
                        var fileWatcher = new FileSystemWatcher(dir, "*");
                        fileWatcher.IncludeSubdirectories = true;
                        fileWatcher.EnableRaisingEvents = true;
                        fileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.DirectoryName | NotifyFilters.LastAccess;
                        fileWatcher.Created += FileWatcher_Created;
                        fileWatcher.Deleted += FileWatcher_Deleted;
                        fileWatcher.Renamed += FileWatcher_Renamed;
                        fileWatcher.Changed += FileWatcher_Changed;
                        _fileWatchers.Add(fileWatcher);

                        _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                        {
                            Folders.Add(folder);

                        });
                    }
                }

                _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                {
                    Status = "Ready";
                    ScanVisibility = Visibility.Collapsed;
                });
            });

        }

        public void BuildNow(Object obj)
        {
            Task.Run(() =>
            {
                _consoleWriter.AddMessage(LogType.Message, "Starting build");
                _consoleWriter.Flush(true);
                var result = _builder.BuildAll("release", 1, 2);
                if (result.Successful)
                {
                    _consoleWriter.AddMessage(LogType.Success, "Build Succeeded");
                }
                else
                {
                    _consoleWriter.AddMessage(LogType.Error, "Build Failed!");
                }
                _consoleWriter.Flush(false);
            });
        }

        #region File Watcher
        private List<string> _ignoredFileTypes = new List<string>()
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

        private Dictionary<string, DateTime> _updateTimeStamps = new Dictionary<string, DateTime>();

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
            var extension = Path.GetExtension(fullFileName).ToLower();
            if (ShouldIgnore(directoryName, fullFileName, changeType)) return;


            var folder = Folders.Where(fld => fld.Path == directoryName).FirstOrDefault();
            if (folder != null)
            {
                var added = false;

                var file = folder.NotStaged.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                if (file == null) file = folder.Untracked.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                if (file == null) file = folder.Conflicted.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                if (file == null) file = folder.Staged.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                if (file == null)
                {
                    file = new GitFileStatus()
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
                            folder.NotStaged.Remove(file);
                            folder.NotStagedFileStatus = folder.NotStaged.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
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
                                folder.Untracked.Add(file);
                                break;
                            case GitFileState.Staged:
                                break;
                            case GitFileState.Conflicted:
                                break;
                            case GitFileState.NotStaged:
                                folder.NotStaged.Add(file);
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
            var folder = Folders.Where(fld => fld.Path == directoryName).FirstOrDefault();
            if (folder != null)
            {
                _dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate
                {
                    var file = folder.Untracked.Where(fil => fil.FullPath == fullFileName).FirstOrDefault();
                    if (file != null)
                    {
                        folder.Untracked.Remove(file);
                    }

                    folder.UntrackedFileStatus = folder.Untracked.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                    folder.IsDirty = folder.NotStagedFileStatus == CurrentStatus.Dirty || folder.ConflictFileStatus == CurrentStatus.Dirty || folder.UntrackedFileStatus == CurrentStatus.Dirty;
                });
            }
        }

        private bool IsTracked(GitFileStatus status, bool diagnostics = false)
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

        private string DetectChanges(GitFileStatus status, bool diagnostics = false)
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
            if (dir.ToLower().Contains("do.doc"))
            {
                return null;
            }

            var folder = new GitManagedFolder(_dispatcher, _consoleWriter);
            folder.Label = dir.Split('\\').Last();
            folder.Path = dir;
            folder.Scan();

            return folder;
        }

        #region Properties

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

        public ObservableCollection<ConsoleOutput> ConsoleLogOutput { get { return _consoleOutput; } }

        GitFileStatus _currentFile = null;
        public GitFileStatus CurrentFile
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
                        value.Scan(true);

                        _dispatcher.BeginInvoke((Action)delegate
                       {
                           _currentFolder = value;
                           Status = "Ready " + _currentFolder.Label;
                       });
                    });
                }

                
            }
        }
        #endregion

        #region Commands
        public ICommand BuildNowCommand { get; private set; }

        #endregion
    }
}
