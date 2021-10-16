using LagoVista.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LagoVista.GitHelper.Models
{
    public class ViewSettings : ModelBase
    {
        private bool _showSystemChanges = false;
        public bool ShowSystemChanges
        {
            get => _showSystemChanges;
            set
            {
                _showSystemChanges = value;
                Set(ref _showSystemChanges, value);
            }
        }
    }
}
