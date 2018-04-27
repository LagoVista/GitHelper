using LagoVista.Core.Commanding;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LagoVista.Core;
using System.Windows.Threading;
using System.Windows;
using System.Text.RegularExpressions;
using LagoVista.Core.Validation;

namespace LagoVista.GitHelper
{
    public enum GitFolderStates
    {
        UptoDate,
        Unstaged,
        Untracked,
        Conflicts
    }

    public class GitManagedFolder : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private ConsoleWriter _consoleWriter;
        Dispatcher _dispatcher;

        public event EventHandler<bool> IsBusyEvent;

        enum GitStatusParsingState
        {
            Idle,
            Untracked,
            NotStaged,
            Staged,
            Conflicts,
        }

        public GitManagedFolder(Dispatcher dispatcher, ConsoleWriter writer)
        {
            _consoleWriter = writer;
            _dispatcher = dispatcher;

            StashTempFilesCommand = new RelayCommand(StashTempFiles, CanStashTempFiles);
            RestoreTempFilesCommand = new RelayCommand(RestoreTempFiles, CanRestoreTempFiles);
            PushCommand = new RelayCommand(PushFiles, CanPushFiles);
            PullCommand = new RelayCommand(PullFiles, CanPullFiles);
            CommitCommand = new RelayCommand(CommitFiles, CanCommitFiles);
            StageCommand = new RelayCommand(StageFiles, CanStageFiles);
            RefreshCommand = new RelayCommand(Refresh, CanRefresh);
        }

        private void NotifyChanged(string propertyName)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            });
        }

        public bool CanRefresh()
        {
            return !IsBusy;
        }

        public bool CanStashTempFiles()
        {
            return !IsBusy;
        }
        public bool CanStageFiles()
        {
            return (Untracked.Any() || NotStaged.Any()) && !IsBusy;
        }
        public bool CanCommitFiles()
        {
            return Staged.Any() && !IsBusy;
        }

        public bool CanPullFiles()
        {
            return IsBehindOrigin && !IsBusy;
        }
        public bool CanPushFiles()
        {
            return HasUnpushedCommits && !IsBusy;
        }
        public bool CanRestoreTempFiles()
        {
            return StashedFiles.Any() && !IsBusy;
        }

        private void RaiseAllButtonEnabledEvents()
        {
            RefreshCommand.RaiseCanExecuteChanged();
            StashTempFilesCommand.RaiseCanExecuteChanged();
            StageCommand.RaiseCanExecuteChanged();
            CommitCommand.RaiseCanExecuteChanged();
            PushCommand.RaiseCanExecuteChanged();
            PullCommand.RaiseCanExecuteChanged();
            RestoreTempFilesCommand.RaiseCanExecuteChanged();
        }

        #region Command Handlers
        public void StashTempFiles()
        {
            IsBusy = true;
            _consoleWriter.AddMessage(LogType.Message, "Stashing temporary files.");
            _consoleWriter.Flush(true);

            var tempPath = System.IO.Path.GetTempPath();
            tempPath = System.IO.Path.Combine(tempPath, Label);
            if (!System.IO.Directory.Exists(tempPath))
            {
                System.IO.Directory.CreateDirectory(tempPath);
            }

            foreach (var file in NotStaged.Where(fil => !fil.IsDirty))
            {
                StashedFiles.Add(file);
            }

            foreach (var stashedFile in StashedFiles)
            {
                NotStaged.Remove(stashedFile);
                stashedFile.OriginalFullPath = stashedFile.FullPath;
                stashedFile.FullPath = System.IO.Path.Combine(tempPath, $"{Guid.NewGuid().ToId()}.file");

                System.IO.File.Copy(stashedFile.OriginalFullPath, stashedFile.FullPath);
                ResetFileChanges(stashedFile);
                _consoleWriter.AddMessage(LogType.Message, $"Stashed file {stashedFile.Label}");
            }

            _consoleWriter.AddMessage(LogType.Success, $"Done stashing files - ({StashedFiles.Count}) file stashed.");

            _consoleWriter.Flush(false);
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                RestoreTempFilesCommand.RaiseCanExecuteChanged();
                IsBusy = false;
            });
        }

        public void StageFiles()
        {
            IsBusy = true;
            Task.Run(() =>
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git.exe",
                        Arguments = "add . ",
                        UseShellExecute = false,
                        WorkingDirectory = Path,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                _consoleWriter.AddMessage(LogType.Message, $"cd {Path}");
                _consoleWriter.AddMessage(LogType.Message, $"{proc.StartInfo.FileName} {proc.StartInfo.Arguments}");

                proc.ErrorDataReceived += (sndr, msg) =>
                {
                    var line = proc.StandardError.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Error, line);
                };

                proc.Start();

                while (!proc.StandardOutput.EndOfStream)
                {
                    var line = proc.StandardOutput.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Error, line);
                }

                _consoleWriter.AddMessage(LogType.Message, "------------------------------");
                _consoleWriter.Flush(true);

                Scan(false);
            });
        }

        public void CommitFiles()
        {
            IsBusy = true;

            if (String.IsNullOrEmpty(CommitMessage))
            {
                MessageBox.Show("Commit message is required.");
                return;
            }

            CommitMessage = CommitMessage.Replace('"', '\'');

            Task.Run(() =>
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git.exe",
                        Arguments = $"commit -m \"{CommitMessage}\"",
                        UseShellExecute = false,
                        WorkingDirectory = Path,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                _consoleWriter.AddMessage(LogType.Message, $"cd {Path}");
                _consoleWriter.AddMessage(LogType.Message, $"{proc.StartInfo.FileName} {proc.StartInfo.Arguments}");

                proc.ErrorDataReceived += (sndr, msg) =>
                {
                    var line = proc.StandardError.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Error, line);
                };

                proc.Start();

                while (!proc.StandardOutput.EndOfStream)
                {
                    var line = proc.StandardOutput.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Message, line);
                }


                _consoleWriter.AddMessage(LogType.Message, "------------------------------");
                _consoleWriter.Flush(true);
                CommitMessage = String.Empty;

                Scan(false);
            });
        }


        public void PullFiles()
        {
            IsBusy = true;

            Task.Run(() =>
            {
                _isBusy = true;
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git.exe",
                        Arguments = "pull",
                        UseShellExecute = false,
                        WorkingDirectory = Path,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                _consoleWriter.AddMessage(LogType.Message, $"cd {Path}");
                _consoleWriter.AddMessage(LogType.Message, $"{proc.StartInfo.FileName} {proc.StartInfo.Arguments}");

                proc.ErrorDataReceived += (sndr, msg) =>
                {
                    var line = proc.StandardError.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Error, line);
                };

                proc.Start();

                while (!proc.StandardOutput.EndOfStream)
                {
                    var line = proc.StandardOutput.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Message, line);
                }

                _consoleWriter.AddMessage(LogType.Message, "------------------------------");
                _consoleWriter.Flush(true);
                CommitMessage = String.Empty;

                Scan(false);
            });
        }

        public void Refresh()
        {
            IsBusy = true;

            Task.Run(() =>
            {
                Scan(true);
            });
        }

        public void PushFiles()
        {
            IsBusy = true;

            Task.Run(() =>
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git.exe",
                        Arguments = "push",
                        UseShellExecute = false,
                        WorkingDirectory = Path,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                _consoleWriter.AddMessage(LogType.Message, $"cd {Path}");
                _consoleWriter.AddMessage(LogType.Message, $"{proc.StartInfo.FileName} {proc.StartInfo.Arguments}");
                _consoleWriter.Flush(true);

                proc.ErrorDataReceived += (sndr, msg) =>
                {
                    var line = proc.StandardError.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Error, line);
                };

                proc.Start();

                while (!proc.StandardOutput.EndOfStream)
                {
                    var line = proc.StandardOutput.ReadLine().Trim();
                    _consoleWriter.AddMessage(LogType.Message, line);
                }


                _consoleWriter.AddMessage(LogType.Message, "------------------------------");
                _consoleWriter.Flush(false);

                Scan(false);
            });
        }

        public void RestoreTempFiles()
        {
            IsBusy = true;

            _consoleWriter.AddMessage(LogType.Message, "Stashing temporary files.");
            _consoleWriter.Flush(true);

            foreach (var stashedFile in StashedFiles)
            {
                NotStaged.Add(stashedFile);
                System.IO.File.Delete(stashedFile.OriginalFullPath);
                System.IO.File.Move(stashedFile.FullPath, stashedFile.OriginalFullPath);
                stashedFile.FullPath = stashedFile.OriginalFullPath;
                stashedFile.OriginalFullPath = null;
                _consoleWriter.AddMessage(LogType.Message, $"Restored: {stashedFile.Label}");
            }
            StashedFiles.Clear();
            _consoleWriter.AddMessage(LogType.Success, $"Done restoring files.");

            _consoleWriter.Flush(false);
            IsBusy = false;
        }
        #endregion

        #region Properties
        private string _commitMessage;
        public string CommitMessage
        {
            get { return _commitMessage; }
            set
            {
                _commitMessage = value;
                NotifyChanged(nameof(CommitMessage));
            }
        }

        public GitFolderStates FolderStatus { get; set; }

        public CurrentStatus CurrentStatus { get; private set; } = CurrentStatus.Untouched;

        CurrentStatus _notStagedFileStatus = CurrentStatus.Untouched;
        public CurrentStatus NotStagedFileStatus
        {
            get { return _notStagedFileStatus; }
            set
            {
                _notStagedFileStatus = value;
                NotifyChanged(nameof(NotStagedFileStatus));
            }
        }

        CurrentStatus _conflictFileStatus = CurrentStatus.Untouched;
        public CurrentStatus ConflictFileStatus
        {
            get { return _conflictFileStatus; }
            set
            {
                _conflictFileStatus = value;
                NotifyChanged(nameof(ConflictFileStatus));
            }
        }

        CurrentStatus _untrackedFileStatus = CurrentStatus.Untouched;
        public CurrentStatus UntrackedFileStatus
        {
            get { return _untrackedFileStatus; }
            set
            {
                _untrackedFileStatus = value;
                NotifyChanged(nameof(UntrackedFileStatus));
            }
        }

        CurrentStatus _stagedFileStatus = CurrentStatus.Untouched;
        public CurrentStatus StagedFileStatus
        {
            get { return _stagedFileStatus; }
            set
            {
                _stagedFileStatus = value;
                NotifyChanged(nameof(UntrackedFileStatus));
            }
        }


        private string _label;
        public string Label
        {
            get
            {
                if (String.IsNullOrEmpty(_label))
                {
                    return _label;
                }

                if (BehindOriginCount > 0 || UnpushedCommitCount > 0)
                {
                    return $"{_label} ({BehindOriginCount}/{UnpushedCommitCount}) ";
                }
                else
                {
                    return _label;
                }
            }
            set
            {
                _label = value;
                NotifyChanged(nameof(Label));
            }
        }
        public string Path { get; set; }

        private bool _isDirty = false;
        public bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                _isDirty = value;
                CurrentStatus = value ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                NotifyChanged(nameof(IsDirty));
                NotifyChanged(nameof(CurrentStatus));
            }
        }

        private bool _isBehindOrigin = false;
        public bool IsBehindOrigin
        {
            get { return _isBehindOrigin; }
            set
            {
                _isBehindOrigin = value;
                NotifyChanged(nameof(IsBehindOrigin));
                _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { PullCommand.RaiseCanExecuteChanged(); });
            }
        }

        private bool _hasUnpushedCommits = false;
        public bool HasUnpushedCommits
        {
            get { return _hasUnpushedCommits; }
            set
            {
                _hasUnpushedCommits = value;
                NotifyChanged(nameof(HasUnpushedCommits));
                _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { PushCommand.RaiseCanExecuteChanged(); });

            }
        }

        private int _BehindOriginCount = 0;
        public int BehindOriginCount
        {
            get { return _BehindOriginCount; }
            set
            {
                _BehindOriginCount = value;
                NotifyChanged(nameof(BehindOriginCount));
            }
        }

        private int _unpushedCount = 0;
        public int UnpushedCommitCount
        {
            get { return _unpushedCount; }
            set
            {
                _unpushedCount = value;
                NotifyChanged(nameof(UnpushedCommitCount));
            }
        }


        public ObservableCollection<GitFileStatus> Untracked { get; private set; } = new ObservableCollection<GitFileStatus>();
        public ObservableCollection<GitFileStatus> NotStaged { get; private set; } = new ObservableCollection<GitFileStatus>();
        public ObservableCollection<GitFileStatus> Staged { get; private set; } = new ObservableCollection<GitFileStatus>();
        public ObservableCollection<GitFileStatus> Conflicted { get; private set; } = new ObservableCollection<GitFileStatus>();
        public ObservableCollection<GitFileStatus> FilesToCommit { get { return new ObservableCollection<GitFileStatus>(NotStaged.Where(fil => fil.IsDirty)); } }

        public RelayCommand CommitCommand { get; private set; }
        public RelayCommand PushCommand { get; private set; }
        public RelayCommand PullCommand { get; private set; }
        public RelayCommand StageCommand { get; private set; }
        public RelayCommand RefreshCommand { get; private set; }

        public RelayCommand StashTempFilesCommand { get; private set; }

        public RelayCommand RestoreTempFilesCommand { get; private set; }

        public ObservableCollection<GitFileStatus> StashedFiles { get; private set; } = new ObservableCollection<GitFileStatus>();

        private bool _isBusy = false;
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (_isBusy != value)
                {
                    IsBusyEvent?.Invoke(this, value);
                    NotifyChanged(nameof(IsBusy));
                    _isBusy = value;
                    RaiseAllButtonEnabledEvents();
                }
            }
        }
        #endregion

        #region Utility Methods
        public void ResetFileChanges(GitFileStatus file)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = "checkout " + file.OriginalFullPath,
                    UseShellExecute = false,
                    WorkingDirectory = Path,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            _consoleWriter.AddMessage(LogType.Message, $"git checkout {file.OriginalFullPath}");

            proc.ErrorDataReceived += (sndr, msg) =>
            {
                var line = proc.StandardError.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Error, line);
            };

            IsBehindOrigin = false;

            proc.Start();

            while (!proc.StandardOutput.EndOfStream)
            {
                var line = proc.StandardOutput.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Error, line);
            }

            _consoleWriter.Flush(false);

        }
        #endregion

        #region Scan for changes
        public bool UpdateFromRemote()
        {
            var success = true;

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = "remote update",
                    UseShellExecute = false,
                    WorkingDirectory = Path,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            _consoleWriter.AddMessage(LogType.Message, $"git remote update");
            _consoleWriter.Flush();

            proc.ErrorDataReceived += (sndr, msg) =>
            {
                var line = proc.StandardError.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Error, line);
                success = false;
            };

            proc.Start();

            if (!success)
            {
                _consoleWriter.Flush();
                return false;
            }

            while (!proc.StandardOutput.EndOfStream)
            {
                var line = proc.StandardOutput.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Message, line);
            }

            _consoleWriter.AddMessage(LogType.Success, "Updated from remote.");
            _consoleWriter.AddMessage(LogType.Success, "");
            _consoleWriter.Flush(false);

            return success;
        }

        public bool Scan(bool autoClear = true, bool resetAllClear = true)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                IsBusy = true;
                HasUnpushedCommits = false;
                IsBehindOrigin = false;
                Untracked.Clear();
                NotStaged.Clear();
                Staged.Clear();
                Conflicted.Clear();
                UnpushedCommitCount = 0;
                BehindOriginCount = 0;
            });

            var start = DateTime.Now;

            _consoleWriter.AddMessage(LogType.Message, $"cd {Path}");
            _consoleWriter.Flush(autoClear);

            if (!UpdateFromRemote())
            {
                return false;
            }

            var err = false;

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = "status -uno",
                    UseShellExecute = false,
                    WorkingDirectory = Path,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            _consoleWriter.AddMessage(LogType.Message, $"git status -uno");

            proc.ErrorDataReceived += (sndr, msg) =>
            {
                var line = proc.StandardError.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Error, line);
                err = true;
            };

            proc.Start();

            if (err)
            {
                _consoleWriter.Flush(true);
                return false;
            }

            var scanState = GitStatusParsingState.Idle;

            if (proc.StandardOutput.EndOfStream)
            {
                _consoleWriter.AddMessage(LogType.Warning, "no console output.");
                _consoleWriter.Flush(true);
                return false;
            }

            var untrackedFilesToAdd = new List<GitFileStatus>();
            var notStagedFilesToAdd = new List<GitFileStatus>();
            var stagedFilesToAdd = new List<GitFileStatus>();

            while (!proc.StandardOutput.EndOfStream)
            {
                var line = proc.StandardOutput.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Message, line);

                if (String.IsNullOrEmpty(line.Trim()))
                {
                    continue;
                }

                var behindRegEx = new Regex("Your branch is behind 'origin\\/master' by (\\d+) commit");
                var behindMatch = behindRegEx.Match(line);
                if (behindMatch.Success)
                {
                    IsBehindOrigin = true;
                    if (behindMatch.Groups.Count == 2)
                    {
                        BehindOriginCount = Convert.ToInt32(behindMatch.Groups[1].Value);
                    }
                    continue;
                }

                var aheadRegEx = new Regex("Your branch is ahead of 'origin\\/master' by (\\d+) commit.");
                var match = aheadRegEx.Match(line);
                if (match.Success)
                {
                    HasUnpushedCommits = true;
                    if (match.Groups.Count == 2)
                    {
                        UnpushedCommitCount = Convert.ToInt32(match.Groups[1].Value);
                    }
                    continue;
                }

                if (line.StartsWith("Nothing to commit"))
                {
                    scanState = GitStatusParsingState.Idle;
                    continue;
                }

                if (line == "Changes not staged for commit:")
                {
                    scanState = GitStatusParsingState.NotStaged;
                    continue;
                }

                if (line.StartsWith("Untracked files:"))
                {
                    scanState = GitStatusParsingState.Untracked;
                    continue;
                }

                if (line.StartsWith("Changes to be committed:"))
                {
                    scanState = GitStatusParsingState.Staged;
                    continue;
                }

                if (line.StartsWith("no changes"))
                {
                    scanState = GitStatusParsingState.Idle;
                    continue;
                }

                if (line.StartsWith("Untracked files not listed"))
                {
                    scanState = GitStatusParsingState.Idle;
                    continue;
                }


                if (!String.IsNullOrEmpty(line.Trim()) &&
                    !line.Contains("(use \"git") &&
                    line != "nothing added to commit but untracked files present (use \"git add\" to track)"
                    )
                {
                    var fileType = FileTypes.SourceFile;
                    if (line.EndsWith("nuspec.txt") || line.EndsWith("csproj.bak"))
                    {
                        fileType = FileTypes.TempFile;
                    }

                    if (line.EndsWith("csproj"))
                    {
                        fileType = FileTypes.ProjectFile;
                    }

                    line = line.Replace("modified:", "").Trim().Replace('/', '\\');
                    line = line.Replace("new file:", "").Trim().Replace('/', '\\');
                    var fileStatus = new GitFileStatus(_dispatcher)
                    {
                        Directory = Path,
                        Label = line.Trim(),
                        FileType = fileType,
                        FullPath = $"{Path}\\{line.TrimEnd()}"
                    };

                    switch (scanState)
                    {
                        case GitStatusParsingState.Staged:
                            fileStatus.State = GitFileState.Staged;
                            fileStatus.Changes = DetectChanges(fileStatus);
                            fileStatus.Analyze();

                            stagedFilesToAdd.Add(fileStatus);

                            break;

                        case GitStatusParsingState.NotStaged:
                            {
                                fileStatus.State = GitFileState.NotStaged;
                                fileStatus.Changes = DetectChanges(fileStatus);
                                fileStatus.Analyze();

                                notStagedFilesToAdd.Add(fileStatus);
                            }
                            break;
                        case GitStatusParsingState.Untracked:
                            {
                                fileStatus.State = GitFileState.Untracked;
                                fileStatus.Analyze();

                                untrackedFilesToAdd.Add(fileStatus);
                            }
                            break;
                    }
                }
            }

            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                if (resetAllClear)
                {
                    IsBusy = false;
                }

                foreach (var file in stagedFilesToAdd) Staged.Add(file);
                foreach (var file in untrackedFilesToAdd) Untracked.Add(file);
                foreach (var file in notStagedFilesToAdd) NotStaged.Add(file);

                UntrackedFileStatus = Untracked.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                ConflictFileStatus = Conflicted.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                NotStagedFileStatus = NotStaged.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                StagedFileStatus = Staged.Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                IsDirty = NotStagedFileStatus == CurrentStatus.Dirty ||
                            ConflictFileStatus == CurrentStatus.Dirty ||
                            UntrackedFileStatus == CurrentStatus.Dirty ||
                            StagedFileStatus == CurrentStatus.Dirty ||
                            HasUnpushedCommits || IsBehindOrigin;

                NotifyChanged(nameof(Label));
            });

            _consoleWriter.AddMessage(LogType.Success, $"Update success in {DateTime.Now - start} ");
            _consoleWriter.Flush();

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
        #endregion
    }
}
