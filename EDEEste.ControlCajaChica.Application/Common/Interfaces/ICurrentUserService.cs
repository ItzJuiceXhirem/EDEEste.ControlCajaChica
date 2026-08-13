using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface ICurrentUserService
    {
        public string? UserId { get; }
        public string? UserName { get; }
        public bool IsAuthenticated { get; }
    }
}
