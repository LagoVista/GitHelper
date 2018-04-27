using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace GitHelper.Build
{
    public enum BuildStatus
    {
        NotBuilding,
        Ready,
        Restoring,
        Building,
        Packaging,
        Built,
        Error
    }

    public class SolutionInformation : INotifyPropertyChanged
    {
        Dispatcher _dispatcher;

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetDispatcher(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        private void NotifyChanged(string propertyName)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        public void Reset()
        {
            Status = Build ? BuildStatus.Ready : BuildStatus.NotBuilding;
            StatusMessage = "Waiting";
        }

        public string Name { get; set; }
        public string LocalPath { get; set; }
        public string Repo { get; set; }
        public string Solution { get; set; }
        public bool Private { get; set; }
        public bool Build { get; set; }

        public BuildStatus _status;
        public BuildStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                NotifyChanged(nameof(Status));
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set
            {
                _statusMessage = value;
                NotifyChanged(nameof(StatusMessage));
            }
        }
    }
}
