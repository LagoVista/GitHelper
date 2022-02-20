using LagoVista.Core.Commanding;
using LagoVista.GitHelper.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

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
        private readonly ConsoleWriter _consoleWriter;
        private readonly Dispatcher _dispatcher;
        private readonly ViewSettings _viewSettings;

        public event EventHandler<bool> IsBusyEvent;

        private enum GitStatusParsingState
        {
            Idle,
            Untracked,
            NotStaged,
            Staged,
            Conflicts,
        }

        public GitManagedFolder(Dispatcher dispatcher, ViewSettings viewSettings, ConsoleWriter writer)
        {
            _consoleWriter = writer;
            _dispatcher = dispatcher;
            _viewSettings = viewSettings;

            StashTempFilesCommand = new RelayCommand(StashTempFiles, CanStashFiles);
            StashAllFilesCommand = new RelayCommand(StashAllFiles, CanStashFiles);

            RestoreTempFilesCommand = new RelayCommand(RestoreTempFiles, CanRestoreTempFiles);
            PushCommand = new RelayCommand(PushFiles, CanPushFiles);
            AddCommand = new RelayCommand(AddFile);
            PullCommand = new RelayCommand(PullFiles, CanPullFiles);
            CommitCommand = new RelayCommand(CommitFiles, CanCommitFiles);
            StageCommand = new RelayCommand(StageFiles, CanStageFiles);

            RefreshCommand = new RelayCommand(Refresh, CanRefresh);
            UnstageFileCommand = new RelayCommand(UnStageFile, CanUnstageFile);
            UndoChangesCommand = new RelayCommand(UndoChanges, CanUndoChanges);
            CleanUntrackedCommand = new RelayCommand(CleanChanges, CanCleanUntracked);
            HardResetCommand = new RelayCommand(HardReset, CanHardReset);
            ForcePullCommand = new RelayCommand(ForcePull, CanForcePull);
            MergeCommand = new RelayCommand(Merge);
            DeleteFileCommand = new RelayCommand(DeleteFile);
        }



        private void NotifyChanged(string propertyName)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        public bool CanRefresh()
        {
            return !IsBusy;
        }

        public bool CanCleanUntracked()
        {
            return !IsBusy;
        }

        public bool CanHardReset()
        {
            return !IsBusy;
        }

        private bool CanForcePull()
        {
            return CanPullFiles() && CanPullFiles();
        }

        public bool CanStashFiles()
        {
            return NotStaged.Any() && !IsBusy;
        }

        public bool CanUnstageFile(object obj)
        {
            return !IsBusy;
        }

        public bool CanUndoChanges(object obj)
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
            UndoChangesCommand.RaiseCanExecuteChanged();
            UnstageFileCommand.RaiseCanExecuteChanged();
            RefreshCommand.RaiseCanExecuteChanged();
            StageCommand.RaiseCanExecuteChanged();
            CommitCommand.RaiseCanExecuteChanged();
            PushCommand.RaiseCanExecuteChanged();
            PullCommand.RaiseCanExecuteChanged();
            RestoreTempFilesCommand.RaiseCanExecuteChanged();
            CleanUntrackedCommand.RaiseCanExecuteChanged();
            StashAllFilesCommand.RaiseCanExecuteChanged();
            StashTempFilesCommand.RaiseCanExecuteChanged();
            HardResetCommand.RaiseCanExecuteChanged();
            ForcePullCommand.RaiseCanExecuteChanged();
        }

        #region Command Handlers
        public void StashTempFiles()
        {
            IsBusy = true;
            Task.Run(() =>
            {
                var files = Untracked.Where(fil => fil.CurrentStatus == CurrentStatus.Untouched);

                var bldr = new StringBuilder("stash");
                foreach (var file in files)
                {
                    bldr.Append($" {file.Label}");
                }

                files = NotStaged.Where(fil => fil.CurrentStatus == CurrentStatus.Untouched);
                foreach (var file in files)
                {
                    bldr.Append($" {file.Label} ");
                }

                RunProcess("git.exe", bldr.ToString(), "stashing temporary files", checkRemote: false);
            });
        }

        public void StashAllFiles()
        {
            IsBusy = true;
            Task.Run(() =>
            {
                RunProcess("git.exe", "stash", "stashing all files", checkRemote: false);
            });
        }

        public void AddFile(object obj)
        {
            if (obj is GitManagedFile file)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    RunProcess("git.exe", $"add {file.FullPath}", "adding file", checkRemote: false);
                });
            }
        }

        public void UnStageFile(object obj)
        {
            if (obj is GitManagedFile file)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    RunProcess("git.exe", $"reset {file.FullPath}", "unstaging files", checkRemote: false);
                });
            }
        }

        public void UndoChanges(object obj)
        {
            if (obj is GitManagedFile file)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    RunProcess("git.exe", $"checkout {file.FullPath}", "undo changes", checkRemote: false);
                });
            }
        }

        public void DeleteFile(object obj)
        {
            if (obj is GitManagedFile file)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    try
                    {
                        System.IO.File.Delete(file.FullPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting file: {ex.Message}");
                        _consoleWriter.AddMessage(LogType.Error, $"Error deleting file: {ex.Message}");
                        return;
                    }

                    _consoleWriter.AddMessage(LogType.Success, "Success deleting file.");
                    Scan(false, checkRemote: false);
                });
            }
        }

        public void Merge(object obj)
        {
            if (obj is GitManagedFile file)
            {
                if (MessageBox.Show("In tool resolving of merges is not currently supported.  Would you like to use notepad to manually resolve conflicts?", "Conflicts", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
                {
                    var proc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "notepad.exe",
                            Arguments = file.FullPath,
                            UseShellExecute = false,
                            WorkingDirectory = Path,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                        }
                    };

                    proc.Start();
                    proc.WaitForExit();

                    if (MessageBox.Show("Would you like to add your changes?", "Add Changes", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
                    {
                        IsBusy = true;
                        Task.Run(() =>
                        {
                            RunProcess("git.exe", $"add {file.Label}", "Staged merged changes.", checkRemote: false);
                        });
                    }
                }
            }
        }

        public void StageFiles()
        {
            IsBusy = true;
            Task.Run(() =>
            {
                RunProcess("git.exe", $"add .", "stage files", checkRemote: false);
            });
        }

        public void CommitFiles()
        {
            if (String.IsNullOrEmpty(CommitMessage))
            {
                MessageBox.Show("Commit message is required.");
                return;
            }

            CommitMessage = CommitMessage.Replace('"', '\'');

            IsBusy = true;
            Task.Run(() =>
            {
                RunProcess("git.exe", $"commit -m \"{CommitMessage}\"", "committing files", checkRemote: false);
                _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                {
                    CommitMessage = String.Empty;
                });
                Scan(false);
            });
        }

        public void PullFiles()
        {
            IsBusy = true;
            Task.Run(() =>
            {
                RunProcess("git.exe", $"pull", "pulling files");
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

            IsBusy = true;
            Task.Run(() =>
            {
                RunProcess("git.exe", $"push", "pushing files");
            });
        }

        public void RunProcess(string cmd, string args, string actionType, bool clearConsole = true, bool checkRemote = true)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = args,
                    UseShellExecute = false,
                    WorkingDirectory = Path,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _consoleWriter.AddMessage(LogType.Message, $"cd {Path}");
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
            _consoleWriter.Flush(clearConsole);

            Scan(false, checkRemote: checkRemote);
        }

        public void CleanChanges()
        {
            if (MessageBox.Show("Are you absolutely sure, this can not be undone and you may lose work.", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    RunProcess("git.exe", "clean -fx", "clean changes.", true, checkRemote: false);
                });
            }
        }

        public async void ForcePull()
        {
            if (MessageBox.Show("Are you absolutely sure, this can not be undone and you may lose work, both untracked, not staged and commited files will be removed.", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                IsBusy = true;
                await Task.Run(() =>
                {
                    RunProcess("git.exe", "reset --hard", "reset hard.", true, checkRemote: false);
                    RunProcess("git.exe", $"pull", "pulling files");
                    Scan(false, checkRemote: false);
                });
            }
        }

        public void HardReset()
        {
            if (MessageBox.Show("Are you absolutely sure, this can not be undone and you may lose work, both untracked, not staged and commited files will be removed.", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    RunProcess("git.exe", "reset --hard", "reset hard.", true, checkRemote: false);
                    Scan(false, checkRemote: false);
                });
            }
        }

        public void RestoreTempFiles()
        {
            IsBusy = true;
            Task.Run(() =>
            {
                RunProcess("git.exe", "stash apply", "restoring stash", true, checkRemote: false);
                RunProcess("git.exe", "stash drop", "restoring stash", false, checkRemote: false);
            });

        }
        #endregion

        #region Properties
        private string _commitMessage;
        public string CommitMessage
        {
            get => _commitMessage;
            set
            {
                _commitMessage = value;
                NotifyChanged(nameof(CommitMessage));
            }
        }

        public GitFolderStates FolderStatus { get; set; }

        public CurrentStatus CurrentStatus { get; private set; } = CurrentStatus.Untouched;

        private CurrentStatus _notStagedFileStatus = CurrentStatus.Untouched;
        public CurrentStatus NotStagedFileStatus
        {
            get => _notStagedFileStatus;
            set
            {
                _notStagedFileStatus = value;
                NotifyChanged(nameof(NotStagedFileStatus));
            }
        }

        private CurrentStatus _conflictFileStatus = CurrentStatus.Untouched;
        public CurrentStatus ConflictFileStatus
        {
            get => _conflictFileStatus;
            set
            {
                _conflictFileStatus = value;
                NotifyChanged(nameof(ConflictFileStatus));
            }
        }

        private CurrentStatus _untrackedFileStatus = CurrentStatus.Untouched;
        public CurrentStatus UntrackedFileStatus
        {
            get => _untrackedFileStatus;
            set
            {
                _untrackedFileStatus = value;
                NotifyChanged(nameof(UntrackedFileStatus));
            }
        }

        private CurrentStatus _stagedFileStatus = CurrentStatus.Untouched;
        public CurrentStatus StagedFileStatus
        {
            get => _stagedFileStatus;
            set
            {
                _stagedFileStatus = value;
                NotifyChanged(nameof(StagedFileStatus));
            }
        }

        private CurrentStatus _stashedFiles = CurrentStatus.Untouched;
        public CurrentStatus StashedFileStatus
        {
            get => _stashedFiles;
            set
            {
                _stashedFiles = value;
                NotifyChanged(nameof(StashedFileStatus));
            }
        }


        private string _label;
        public string Label
        {
            get => String.IsNullOrEmpty(_label)
                    ? _label
                    : BehindOriginCount > 0 || UnpushedCommitCount > 0
                    ? $"{_label} ({BehindOriginCount}/{UnpushedCommitCount}) "
                    : _label;
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
            get => _isDirty;
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
            get => _isBehindOrigin;
            set
            {
                _isBehindOrigin = value;
                NotifyChanged(nameof(IsBehindOrigin));
                _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                {
                    PullCommand.RaiseCanExecuteChanged();
                    ForcePullCommand.RaiseCanExecuteChanged();
                });
            }
        }

        private bool _hasUnpushedCommits = false;
        public bool HasUnpushedCommits
        {
            get => _hasUnpushedCommits;
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
            get => _BehindOriginCount;
            set
            {
                _BehindOriginCount = value;
                NotifyChanged(nameof(BehindOriginCount));
            }
        }

        private int _unpushedCount = 0;
        public int UnpushedCommitCount
        {
            get => _unpushedCount;
            set
            {
                _unpushedCount = value;
                NotifyChanged(nameof(UnpushedCommitCount));
            }
        }

        private ObservableCollection<GitManagedFile> _untracked;
        public ObservableCollection<GitManagedFile> Untracked
        {
            get => _untracked;
            set
            {
                _untracked = value;
                NotifyChanged(nameof(Untracked));
            }
        }

        private ObservableCollection<GitManagedFile> _notStaged;
        public ObservableCollection<GitManagedFile> NotStaged
        {
            get => _notStaged;
            set
            {
                _notStaged = value;
                NotifyChanged(nameof(NotStaged));
                NotifyChanged(nameof(FilesToCommit));
            }
        }

        private ObservableCollection<GitManagedFile> _staged;
        public ObservableCollection<GitManagedFile> Staged
        {
            get => _staged;
            set
            {
                _staged = value;
                NotifyChanged(nameof(Staged));
            }
        }

        private ObservableCollection<GitManagedFile> _stashed;
        public ObservableCollection<GitManagedFile> StashedFiles
        {
            get => _stashed;
            set
            {
                _stashed = value;
                NotifyChanged(nameof(StashedFiles));
            }
        }

        private ObservableCollection<GitManagedFile> _conflicted;
        public ObservableCollection<GitManagedFile> Conflicted
        {
            get => _conflicted;
            set
            {
                _conflicted = value;
                NotifyChanged(nameof(Conflicted));
            }
        }

        public ObservableCollection<GitManagedFile> FilesToCommit => NotStaged != null ? new ObservableCollection<GitManagedFile>(NotStaged.Where(fil => fil.IsDirty)) : null;

        public RelayCommand CommitCommand { get; private set; }
        public RelayCommand PushCommand { get; private set; }
        public RelayCommand PullCommand { get; private set; }
        public RelayCommand StageCommand { get; private set; }
        public RelayCommand UnstageFileCommand { get; private set; }
        public RelayCommand UndoChangesCommand { get; private set; }
        public RelayCommand MergeCommand { get; private set; }
        public RelayCommand DeleteFileCommand { get; private set; }
        public RelayCommand StashAllFilesCommand { get; private set; }
        public RelayCommand AddCommand { get; private set; }
        public RelayCommand RefreshCommand { get; private set; }

        public RelayCommand StashTempFilesCommand { get; private set; }

        public RelayCommand RestoreTempFilesCommand { get; private set; }

        public RelayCommand CleanUntrackedCommand { get; private set; }
        public RelayCommand HardResetCommand { get; private set; }
        public RelayCommand ForcePullCommand { get; private set; }

        private bool _isBusy = false;
        public bool IsBusy
        {
            get => _isBusy;
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
        public void ResetFileChanges(GitManagedFile file)
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

        public bool Scan(bool autoClear = true, bool resetAllClear = true, bool checkRemote = true)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                IsBusy = true;
                HasUnpushedCommits = false;
                IsBehindOrigin = false;

                Untracked = null;
                NotStaged = null;
                StashedFiles = null;
                Staged = null;
                Conflicted = null;

                UnpushedCommitCount = 0;
                BehindOriginCount = 0;
            });

            var start = DateTime.Now;

            _consoleWriter.AddMessage(LogType.Message, $"cd {Path}");
            _consoleWriter.Flush(autoClear);

            if (checkRemote)
            {
                if (!UpdateFromRemote())
                {
                    return false;
                }
            }

            var err = false;

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = "status -u",
                    UseShellExecute = false,
                    WorkingDirectory = Path,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            _consoleWriter.AddMessage(LogType.Message, $"git status -u");

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

            var untrackedFilesToAdd = new List<GitManagedFile>();
            var notStagedFilesToAdd = new List<GitManagedFile>();
            var stagedFilesToAdd = new List<GitManagedFile>();
            var stashedFilesToAdd = new List<GitManagedFile>();
            var conflictedFilesToAdd = new List<GitManagedFile>();

            while (!proc.StandardOutput.EndOfStream)
            {
                var line = proc.StandardOutput.ReadLine().Trim();
                _consoleWriter.AddMessage(LogType.Message, line);

                if (String.IsNullOrEmpty(line.Trim()))
                {
                    continue;
                }

                var bothChangedRegEx = new Regex(@"and have (?'localcommits'\d+) and (?'remotecommits'\d+) different commits each");
                var bothChangedMatch = bothChangedRegEx.Match(line);
                if (bothChangedMatch.Success)
                {
                    IsBehindOrigin = true;
                    HasUnpushedCommits = true;
                    UnpushedCommitCount = Convert.ToInt32(bothChangedMatch.Groups["localcommits"].Value);
                    BehindOriginCount = Convert.ToInt32(bothChangedMatch.Groups["remotecommits"].Value);
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

                if (line.StartsWith("Unmerged paths:"))
                {
                    scanState = GitStatusParsingState.Conflicts;
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

                    var changeType = ChangeType.None;

                    if (line.StartsWith("both")) changeType = ChangeType.BothModified;
                    if (line.StartsWith("modified")) changeType = ChangeType.Modified;
                    if (line.StartsWith("new file")) changeType = ChangeType.New;
                    if (line.StartsWith("deleted")) changeType = ChangeType.Deleted;

                    line = line.Replace("both modified:", "").Trim().Replace('/', '\\');
                    line = line.Replace("modified:", "").Trim().Replace('/', '\\');
                    line = line.Replace("new file:", "").Trim().Replace('/', '\\');
                    line = line.Replace("deleted:", "").Trim().Replace('/', '\\');

                    var fileStatus = new GitManagedFile(_dispatcher, _viewSettings, this)
                    {
                        Directory = Path,
                        Label = line.Trim(),
                        FileType = fileType,
                        ChangeType = changeType,
                        FullPath = $"{Path}\\{line.TrimEnd()}"
                    };

                    if (changeType == ChangeType.Deleted)
                    {
                        fileStatus.IsDirty = true;
                    }

                    switch (scanState)
                    {
                        case GitStatusParsingState.Conflicts:
                            fileStatus.State = GitFileState.Conflicted;
                            fileStatus.Changes = DetectChanges(fileStatus);
                            conflictedFilesToAdd.Add(fileStatus);
                            break;
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
                                if (changeType != ChangeType.Deleted)
                                {
                                    fileStatus.Analyze();
                                }

                                if (_viewSettings.ShowSystemChanges || fileStatus.IsDirty)
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

            proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = "stash show",
                    UseShellExecute = false,
                    WorkingDirectory = Path,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            _consoleWriter.AddMessage(LogType.Message, $"git stash show");

            proc.Start();

            if (err)
            {
                _consoleWriter.Flush(true);
                return false;
            }

            var nonChangeRegEx = new Regex(@"\s*\d+\s*files changed");

            while (!proc.StandardOutput.EndOfStream)
            {
                var line = proc.StandardOutput.ReadLine();
                _consoleWriter.AddMessage(LogType.Message, line);

                if (!nonChangeRegEx.Match(line).Success &&
                    line != "No stash entries found." &&
                    !String.IsNullOrEmpty(line))
                {
                    stashedFilesToAdd.Add(new GitManagedFile(_dispatcher, _viewSettings, this)
                    {
                        Label = line,
                        State = GitFileState.Stashed,
                        Directory = Path
                    });
                }
            }

            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                Staged = new ObservableCollection<GitManagedFile>();
                Untracked = new ObservableCollection<GitManagedFile>();
                StashedFiles = new ObservableCollection<GitManagedFile>();
                NotStaged = new ObservableCollection<GitManagedFile>();
                Conflicted = new ObservableCollection<GitManagedFile>();

                foreach (var file in stagedFilesToAdd)
                {
                    Staged.Add(file);
                }

                foreach (var file in untrackedFilesToAdd)
                {
                    Untracked.Add(file);
                }

                foreach (var file in notStagedFilesToAdd)
                {
                    NotStaged.Add(file);
                }

                foreach (var file in stashedFilesToAdd)
                {
                    StashedFiles.Add(file);
                }

                foreach (var file in conflictedFilesToAdd)
                {
                    Conflicted.Add(file);
                }

                if (Conflicted.Any())
                {
                    IsDirty = true;
                    CurrentStatus = CurrentStatus.Conflicts;
                    ConflictFileStatus = CurrentStatus.Conflicts;
                }
                else
                {
                    ConflictFileStatus = CurrentStatus.Untouched;
                    UntrackedFileStatus = Untracked.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                    NotStagedFileStatus = NotStaged.Where(utf => utf.IsDirty).Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                    StashedFileStatus = StashedFiles.Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                    StagedFileStatus = Staged.Any() ? CurrentStatus.Dirty : CurrentStatus.Untouched;
                    IsDirty = NotStagedFileStatus == CurrentStatus.Dirty ||
                                ConflictFileStatus == CurrentStatus.Dirty ||
                                UntrackedFileStatus == CurrentStatus.Dirty ||
                                StagedFileStatus == CurrentStatus.Dirty ||
                                StashedFileStatus == CurrentStatus.Dirty ||
                                HasUnpushedCommits || IsBehindOrigin;
                }

                if (resetAllClear)
                {
                    IsBusy = false;
                }

                NotifyChanged(nameof(Label));
            });

            _consoleWriter.AddMessage(LogType.Success, $"Update success in {DateTime.Now - start} ");
            _consoleWriter.Flush();

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
                var line = proc.StandardOutput.ReadLine().Trim();
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
