using Abhyanvaya.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Domain.Entities
{
    public class Language : BaseEntity
    {
        public string Name { get; set; }   // Hindi, Sanskrit, Telugu
    }
}
