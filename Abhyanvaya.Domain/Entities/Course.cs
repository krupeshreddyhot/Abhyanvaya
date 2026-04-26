using Abhyanvaya.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abhyanvaya.Domain.Entities
{
    public class Course : BaseEntity
    {
        public string Code { get; set; }   // BCOM, BSC, BBA
        public string Name { get; set; }   // B.Com, B.Sc, BBA
    }
}
