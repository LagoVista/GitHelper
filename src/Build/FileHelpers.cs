using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelper.Build
{
    public interface IFileHelper
    {
        string OpenFile(string fileName);

        void WriteFile(string fileName, string contents);
    }

    public class FileHelpers : IFileHelper
    {
        public string OpenFile(string fileName)
        {
            return System.IO.File.ReadAllText(fileName);
        }

        public void WriteFile(string fileName, string contents)
        {
            System.IO.File.WriteAllText(fileName, contents);
        }
    }
}
