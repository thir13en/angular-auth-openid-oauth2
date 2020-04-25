using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SecuringAngularApps.API.Model
{
    public class AuthContext
    {
        public List<SimpleClaim> Claims { get; set; }
        public UserProfile UserProfile { get; set; }
    }
}
