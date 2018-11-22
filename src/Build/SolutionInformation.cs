using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Threading;

namespace GitHelper.Build
{
    public enum BuildStatus
    {
        Skipping,
        Ready,
        Restoring,
        Building,
        Packaging,
        Built,
        Error
    }

    public class SolutionInformation : INotifyPropertyChanged
    {
        private Dispatcher _dispatcher;

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetDispatcher(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        private void NotifyChanged(string propertyName)
        {
            _dispatcher?.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        public void Reset()
        {
            Status = ShouldBuild ? BuildStatus.Ready : BuildStatus.Skipping;
            StatusMessage = "Waiting";
        }

        public string Name { get; set; }
        public string LocalPath { get; set; }
        public string Repo { get; set; }
        public string Solution { get; set; }
        public bool Private { get; set; }
        public bool ShouldBuild { get; set; }

        public List<string> Packages { get; set; } = new List<string>();

        private bool _build = true;
        public bool Build
        {
            get => _build;
            set
            {
                _build = value;
                NotifyChanged(nameof(Build));
            }
        }

        public BuildStatus _status;
        public BuildStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                NotifyChanged(nameof(Status));
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                NotifyChanged(nameof(StatusMessage));
            }
        }
    }
}
