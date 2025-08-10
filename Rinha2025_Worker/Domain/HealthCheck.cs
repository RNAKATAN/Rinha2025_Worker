using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rinha2025_Worker.Domain
{
    public class HealthCheck
    {
        public bool Failing { get; set; }

        public int MinResponseTime { get; set; }
    }
}
