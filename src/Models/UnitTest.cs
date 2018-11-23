using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.GitHelper.Models
{
    public class UnitTest
    {
        public UnitTest(string fullPath)
        {
            FullPath = fullPath;
        }

        public string FullPath { get; set; }

        public int TestRan { get; set; }
        public int TestSuccess { get; set; }
        public int TestFailed { get; set; }


        public override string ToString()
        {
            return FullPath.Substring(FullPath.LastIndexOf("/") + 1);
        }
    }
}
