using LagoVista.GitHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelperTests
{
    public class ConsoleWriter : IConsoleWriter
    {
        public void AddMessage(LogType type, string message)
        {
            Console.WriteLine(type.ToString() + " " + message);
        }

        public void Flush(bool clear = false)
        {
            
        }
    }
}
