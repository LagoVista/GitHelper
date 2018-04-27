using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace LagoVista.GitHelper
{
    public enum GitFileState
    {
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
        Dirty,
        Untouched
    }

    public class GitFileStatus : INotifyPropertyChanged
    {
        Dispatcher _dispatcher;

        public GitFileStatus(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
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
    }
}
