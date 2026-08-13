using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<string?> GetUserNameAsync(string userId);
        Task<bool> IsInRoleAsync(string userId, string role);
        Task<(bool Success, string[] Errors)> CreateUserAsync(string email, string password, string nombre, string rol);
        Task CheckRoleAsync(string roleName);
    }
}
