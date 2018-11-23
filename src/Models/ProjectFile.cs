namespace LagoVista.GitHelper.Models
{
    public class ProjectFile
    {
        public string Name
        {
            get
            {
                return FullPath.Substring(FullPath.LastIndexOf(@"\") + 1);
            }
        }

        public string Path
        {
            get
            {
                return FullPath.Substring(0, FullPath.LastIndexOf(@"\"));
            }
        }


        public string Version { get; set; }

        public string FullPath { get; set; }
    }
}
