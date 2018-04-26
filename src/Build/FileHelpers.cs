using LagoVista.Core.Validation;
using LagoVista.GitHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelper.Build
{
    public interface IFileHelper
    {
        InvokeResult<string> OpenFile(string fileName);

        InvokeResult WriteFile(string fileName, string contents);
    }

    public class FileHelpers : IFileHelper
    {
        IConsoleWriter _consoleWriter;

        public FileHelpers(IConsoleWriter writer)
        {
            _consoleWriter = writer;
        }

        public InvokeResult<string> OpenFile(string fileName)
        {
            if(!System.IO.File.Exists(fileName))
            {
                _consoleWriter.AddMessage(LogType.Error, "File does not exist: " + fileName);
                return InvokeResult<string>.FromError("File does not exist: " + fileName);
            }

            try
            {
                return InvokeResult<String>.Create(System.IO.File.ReadAllText(fileName));
            }
            catch(Exception ex)
            {
                _consoleWriter.AddMessage(LogType.Error, "Could not open file: " + fileName + " Exception: " + ex.Message);
                return InvokeResult<string>.FromError("Could not open file: " + fileName + " Exception: " + ex.Message);
            }
        }

        public InvokeResult WriteFile(string fileName, string contents)
        {
            try
            {
                System.IO.File.WriteAllText(fileName, contents);
                return InvokeResult.Success;
            }
            catch(Exception ex)
            {
                _consoleWriter.AddMessage(LogType.Error, "Could not write file: " + fileName + " Exception: " + ex.Message);
                return InvokeResult.FromError("Could not write file: " + fileName + " Exception: " + ex.Message);
            }
        }
    }
}
