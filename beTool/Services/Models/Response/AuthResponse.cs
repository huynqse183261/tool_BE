using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.Response
{
    public class AuthResponse
    {
        public string AccessToken { get; set; }

        public string Email { get; set; }

        public string FullName { get; set; } = string.Empty;
    }
}
