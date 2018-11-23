using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.GitHelper.Models
{
    public class NugetResult
    {
        [JsonProperty("data")]
        public List<NugetPackage> Pacakges { get; set; }        
    }

    public class NugetPackage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("versions")]
        public List<NugetPackageVersion> Versions { get; set; }
    }

    public class NugetPackageVersion
    {
        [JsonProperty("version")]
        public string Version { get; set; }

    }
}
