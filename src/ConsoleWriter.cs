using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LagoVista.GitHelper
{
    public class ConsoleWriter
    {

        ObservableCollection<ConsoleOutput> _buffer = new ObservableCollection<ConsoleOutput>();
        ObservableCollection<ConsoleOutput> _output;
        Dispatcher _dispatcher;

        public ConsoleWriter(ObservableCollection<ConsoleOutput> output, Dispatcher dispatcher)
        {
            _output = output;
            _dispatcher = dispatcher;
        }

        public void AddMessage(LogType type, String message)
        {
            lock (_buffer)
            {
                _buffer.Add(new ConsoleOutput()
                {
                    LogType = type,
                    Output = message
                });
            }
        }

        public void Flush(bool clear = false)
        {
            Collection<ConsoleOutput> tmpBuffer = new Collection<ConsoleOutput>();
            lock (_buffer)
            {
                foreach(var item in _buffer)
                {
                    tmpBuffer.Add(item);
                }
                _buffer.Clear();
            }

            _dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            {
                lock (_output)
                {
                    if (clear)
                    {
                        _output.Clear();
                    }

                    foreach (var msg in tmpBuffer)
                    {
                        _output.Add(msg);
                    }
                }
            });
        }
    }
}
