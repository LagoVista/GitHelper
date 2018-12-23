using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LagoVista.GitHelper.Models
{
    public class UnitTest : INotifyPropertyChanged
    {
        public void RaisePropertyChanged([CallerMemberName] string name = "")
        {
            if (!String.IsNullOrEmpty(name))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }

        }

        public UnitTest(string fullPath)
        {
            FullPath = fullPath;
        }


        public string FullPath { get; set; }

        int _total;
        public int Total
        {
            get { return _total; }
            set
            {
                _total = value;
                RaisePropertyChanged();
            }
        }

        int _passed;
        public int Passed
        {
            get { return _passed; }
            set
            {
                _passed = value;
                RaisePropertyChanged();
            }
        }


        int _failed;
        public int Failed
        {
            get { return _failed; }
            set
            {
                _failed = value;
                RaisePropertyChanged();
            }
        }

        int _skipped;
        public int Skipped
        {
            get { return _skipped; }
            set { _skipped = value; RaisePropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString()
        {
            return FullPath.Substring(FullPath.LastIndexOf("/") + 1);
        }
    }
}
