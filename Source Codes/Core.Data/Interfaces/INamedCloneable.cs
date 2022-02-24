using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Data.Interfaces
{
    public interface INamedCloneable : ICloneable
    {
        string Name { get; set; }
    }
}
