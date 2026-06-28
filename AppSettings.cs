using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp22
{
    public class AppSettings
    {
        public SecurityConfig? Security { get; set; }
    }

    public class SecurityConfig
    {
        public string? AdminPassword { get; set; }
    }
}
