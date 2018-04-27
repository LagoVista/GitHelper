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
            AddCommand = new RelayCommand(AddFile);
            PullCommand = new RelayCommand(PullFiles, CanPullFiles);
            CommitCommand = new RelayCommand(CommitFiles, CanCommitFiles);
            StageCommand = new RelayCommand(StageFiles, CanStageFiles);
            RefreshCommand = new RelayCommand(Refresh, CanRefresh);
            UnstageFileCommand = new RelayCommand(UnStageFile, CanUnstageFile);
            UndoChangesCommand = new RelayCommand(UndoChanges, CanUndoChanges);
            CleanUntrackedCommand = new RelayCommand(CleanChanges, CanCleanUntracked);
            HardResetCommand = new RelayCommand(HardReset, CanHardReset);
            MergeCommand = new RelayCommand(Merge);
            DeleteFileCommand = new RelayCommand(DeleteFile);
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

        public bool CanCleanUntracked()
        {
            return !IsBusy;
        }

        public bool CanHardReset()
        {
            return !IsBusy;
        }

        public bool CanStashTempFiles()
        {
            return NotStaged.Any() && !IsBusy;
        }

        public bool CanUnstageFile(Object obj)
        {
            return !IsBusy;
        }

        public bool CanUndoChanges(Object obj)
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
            StashTempFilesCommand.RaiseCanExecuteChanged();
            StageCommand.RaiseCanExecuteChanged();
            CommitCommand.RaiseCanExecuteChanged();
            PushCommand.RaiseCanExecuteChanged();
            PullCommand.RaiseCanExecuteChanged();
            RestoreTempFilesCommand.RaiseCanExecuteChanged();
            CleanUntrackedCommand.RaiseCanExecuteChanged();
        }

        #region Command Handlers
        public void StashTempFiles()
        {
            IsBusy = true;
            Task.Run(() =>
            {
                RunProcess("git.exe", "stash", "stashing files", checkRemote: false);
            });
        }

        public void AddFile(Object obj)
        {
            if (obj is GitFileStatus file)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    RunProcess("git.exe", $"add {file.FullPath}", "adding file", checkRemote: false);
                });
            }
        }

        public void UnStageFile(Object obj)
        {
            if (obj is GitFileStatus file)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    RunProcess("git.exe", $"reset {file.FullPath}", "unstaging files", checkRemote: false);
                });
            }
        }

        public void UndoChanges(Object obj)
        {
            if (obj is GitFileStatus file)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    RunProcess("git.exe", $"checkout {file.FullPath}", "undo changes", checkRemote:false);
                });
            }
        }

        public void DeleteFile(Object obj)
        {
            if (obj is GitFileStatus file)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    try
                    {
                        System.IO.File.Delete(file.FullPath);
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show("Error deleting file");
                        _consoleWriter.AddMessage(LogType.Error, "Error deleting file.");
                        return;
                    }

                    _consoleWriter.AddMessage(LogType.Success, "Success deleting file.");
                    Scan(false, checkRemote: false);
                });
            }
        }

        public void Merge(Object obj)
        {
            if (obj is GitFileStatus file)
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

                    if(MessageBox.Show("Would you like to add your changes?", "Add Changes", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
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
                RunProcess("git.exe", $"commit -m \"{CommitMessage}\"", "committing files", checkRemote:false);
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

        private void RunProcess(string cmd, string args, string actionType, bool clearConsole = true, bool checkRemote = true)
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

            Scan(false, checkRemote:checkRemote);
        }

        public void CleanChanges()
        {
            if(MessageBox.Show("Are you absolutely sure, this can not be undone and you may lose work.", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                IsBusy = true;
                Task.Run(() =>
                {
                    RunProcess("git.exe", "clean -fx", "clean changes.", true, checkRemote:false);
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
                    RunProcess("git.exe", "clean -fx", "clean changes.", true, checkRemote: false);
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
                NotifyChanged(nameof(StagedFileStatus));
            }
        }

        CurrentStatus _stashedFiles = CurrentStatus.Untouched;
        public CurrentStatus StashedFileStatus
        {
            get { return _stashedFiles; }
            set
            {
                _stashedFiles = value;
                NotifyChanged(nameof(StashedFileStatus));
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
        public ObservableCollection<GitFileStatus> Stashed { get; private set; } = new ObservableCollection<GitFileStatus>();
        public ObservableCollection<GitFileStatus> Conflicted { get; private set; } = new ObservableCollection<GitFileStatus>();
        public ObservableCollection<GitFileStatus> FilesToCommit { get { return new ObservableCollection<GitFileStatus>(NotStaged.Where(fil => fil.IsDirty)); } }

        public RelayCommand CommitCommand { get; private set; }
        public RelayCommand PushCommand { get; private set; }
        public RelayCommand PullCommand { get; private set; }
        public RelayCommand StageCommand { get; private set; }
        public RelayCommand UnstageFileCommand { get; private set; }
        public RelayCommand UndoChangesCommand { get; private set; }
        public RelayCommand MergeCommand { get; private set; }
        public RelayCommand DeleteFileCommand { get; private set; }
        public RelayCommand AddCommand { get; private set; }
        public RelayCommand RefreshCommand { get; private set; }

        public RelayCommand StashTempFilesCommand { get; private set; }

        public RelayCommand RestoreTempFilesCommand { get; private set; }

        public RelayCommand CleanUntrackedCommand { get; private set; }
        public RelayCommand HardResetCommand { get; private set; }

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

        public bool Scan(bool autoClear = true, bool resetAllClear = true, bool checkRemote = true)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                IsBusy = true;
                HasUnpushedCommits = false;
                IsBehindOrigin = false;
                Untracked.Clear();
                NotStaged.Clear();
                StashedFiles.Clear();
                Staged.Clear();
                Conflicted.Clear();
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

            var untrackedFilesToAdd = new List<GitFileStatus>();
            var notStagedFilesToAdd = new List<GitFileStatus>();
            var stagedFilesToAdd = new List<GitFileStatus>();
            var stashedFilesToAdd = new List<GitFileStatus>();
            var conflictedFilesToAdd = new List<GitFileStatus>();

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
                if(bothChangedMatch.Success)
                {
                    IsBehindOrigin = true;
                    HasUnpushedCommits = true;
                    UnpushedCommitCount  = Convert.ToInt32(bothChangedMatch.Groups["localcommits"].Value);
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

                    line = line.Replace("both modified:", "").Trim().Replace('/', '\\');
                    line = line.Replace("modified:", "").Trim().Replace('/', '\\');
                    line = line.Replace("new file:", "").Trim().Replace('/', '\\');
                    var fileStatus = new GitFileStatus(_dispatcher, this)
                    {
                        Directory = Path,
                        Label = line.Trim(),
                        FileType = fileType,
                        FullPath = $"{Path}\\{line.TrimEnd()}"
                    };

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
                    stashedFilesToAdd.Add(new GitFileStatus(_dispatcher, this)
                    {
                        Label = line,
                        State = GitFileState.Stashed,
                        Directory = Path
                    });
                }
            }

            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {

                foreach (var file in stagedFilesToAdd) Staged.Add(file);
                foreach (var file in untrackedFilesToAdd) Untracked.Add(file);
                foreach (var file in notStagedFilesToAdd) NotStaged.Add(file);
                foreach (var file in stashedFilesToAdd) StashedFiles.Add(file);
                foreach (var file in conflictedFilesToAdd) Conflicted.Add(file);

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
