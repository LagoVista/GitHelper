using System.Collections.ObjectModel;

namespace LagoVista.GitHelper.Models
{
    public class PackageVersion
    {
        public string Version { get; set; }

        public bool IsPrerelease
        {
            get
            {
                return Version.Contains("-");
            }
        }

        public ObservableCollection<ProjectFile> ProjectFiles { get; set; } = new ObservableCollection<ProjectFile>();

        public override string ToString()
        {
            return Version;
        }
    }
}
