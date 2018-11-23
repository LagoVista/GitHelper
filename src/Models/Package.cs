using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace LagoVista.GitHelper.Models
{
    public class Package : INotifyPropertyChanged
    {
        public string Current { get; set; }
        public string Prerelease { get; set; }
        public string Installed
        {
            get
            {
                if(VersionCount == 1)
                {
                    return $"({InstalledVersions.First().Version})";
                }
                else
                {
                    return $"(Multiple)";
                }
            }
        }

        public string Name { get; set; }

        public int VersionCount { get { return InstalledVersions.Count; } }

        public ObservableCollection<PackageVersion> InstalledVersions { get; set; } = new ObservableCollection<PackageVersion>();


        ObservableCollection<PackageVersion> _allVersions = new ObservableCollection<PackageVersion>();
        public ObservableCollection<PackageVersion> AllVersions 
        {
            get { return new ObservableCollection<PackageVersion>( _allVersions.OrderByDescending(ver=>ver.Version)); }
        }



        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private bool _allowPrerelease = false;
        public bool AllowPrelease
        {
            get { return _allowPrerelease; }
            set
            {
                _allowPrerelease = value;
                NotifyChanged(nameof(AllowPrelease));
                NotifyChanged(nameof(CanUpgarde));
            }
        }

        public bool CanUpgarde
        {
            get
            {
                if (InstalledVersions.Count == 0)
                    return false;

                return InstalledVersions.Count > 1 ||
                    ((InstalledVersions.First().Version != Current && !AllowPrelease) ||
                        (InstalledVersions.First().Version != Prerelease && AllowPrelease));
            }
        }

        PackageVersion _selectedVersion;
        public PackageVersion SelectedVersion
        {
            get { return _selectedVersion; }
            set
            {
                _selectedVersion = value;
                NotifyChanged(nameof(SelectedVersion));
            }
        }

        public void AddVersion(PackageVersion version)
        {
            _allVersions.Add(version);
        }

        public void AddInstalledVersion(PackageVersion version)
        {
            InstalledVersions.Add(version);
        }
    }
}
