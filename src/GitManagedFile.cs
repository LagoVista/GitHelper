using LagoVista.Core.Commanding;
using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace LagoVista.GitHelper
{
    public enum GitFileState
    {
        Stashed,
        Untracked,
        NotStaged,
        Staged,
        Conflicted,
    }

    public enum FileTypes
    {
        TempFile,
        SourceFile,
        ProjectFile
    }

    public enum CurrentStatus
    {
        Conflicts,
        Dirty,
        Untouched
    }

    public class GitFileStatus : INotifyPropertyChanged
    {
        Dispatcher _dispatcher;

        GitManagedFolder _folder;

        public GitFileStatus(Dispatcher dispatcher, GitManagedFolder folder)
        {
            _dispatcher = dispatcher;
            _folder = folder;

            UnstageFileCommand = new RelayCommand((obj) => _folder.UnstageFileCommand.Execute(this));
            UndoChangesCommand = new RelayCommand((obj) => _folder.UndoChangesCommand.Execute(this));
            MergeCommand = new RelayCommand((obj) => _folder.MergeCommand.Execute(this));
            AddCommand = new RelayCommand((obj) => _folder.AddCommand.Execute(this));
            DeleteCommand = new RelayCommand((obj) => _folder.DeleteFileCommand.Execute(this));
        }

        private void NotifyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                _dispatcher.BeginInvoke((Action)delegate
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                });
            }
        }

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

        public GitFileState State { get; set; }

        public string Directory { get; set; }

        public string FullPath { get; set; }


        public string OriginalFullPath { get; set; }

        public string Label { get; set; }

        public FileTypes FileType { get; set; }


        public CurrentStatus CurrentStatus { get; private set; } = CurrentStatus.Untouched;

        private string _changes;
        public string Changes
        {
            get { return _changes; }
            set
            {
                if (_changes != value)
                {
                    _changes = value;
                    NotifyChanged(nameof(Changes));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Analyze()
        {
            IsDirty = false;

            if (State == GitFileState.Untracked)
            {
                IsDirty = (FileType != FileTypes.TempFile);
                if (System.IO.File.Exists(FullPath))
                {
                    Changes = System.IO.File.ReadAllText(FullPath);
                }
                else
                {
                    Changes = "-no content-";
                }
                return;
            }

            if (String.IsNullOrEmpty(Changes))
            {
                IsDirty = false;
                return;
            }

            if(State == GitFileState.Conflicted)
            {
                CurrentStatus = CurrentStatus.Conflicts;
            }

            if (State == GitFileState.Staged)
            {
                IsDirty = true;
                return;
            }

            var lines = Changes.Split('\r');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (!String.IsNullOrEmpty(trimmedLine))
                {

                    var regEx = new Regex("<PackageReference.+Include=\"LagoVista.+\".+./>");
                    if (!regEx.Match(trimmedLine).Success)
                    {
                        if (!String.IsNullOrEmpty(Changes) && !Label.EndsWith("nuspec"))
                        {
                            IsDirty = true;
                        }
                    }
                }
            }            
        }

        public GitManagedFolder Folder { get { return _folder; } }

        public RelayCommand UndoChangesCommand { get; private set; }
        public RelayCommand UnstageFileCommand { get; private set; }
        public RelayCommand MergeCommand { get; private set; }
        public RelayCommand AddCommand { get; private set; }
        public RelayCommand DeleteCommand { get; private set; }
    }
}
