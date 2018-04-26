using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace LagoVista.GitHelper
{
    public enum LogType
    {
        Message,
        Warning,
        Error,
    }

    public class ConsoleOutput
    {
        public string Output { get; set; }
        public LogType LogType { get; set; }
    }
}
