using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelper.Build
{
    public class SolutionInformation
    {
        public string Name { get; set; }
        public string LocalPath { get; set; }
        public string Repo { get; set; }
        public string Solution { get; set; }
        public bool Private { get; set; }
        public bool Build { get; set; }
    }
}
