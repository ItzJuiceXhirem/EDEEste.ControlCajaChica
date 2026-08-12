using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Identity;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Infrastructure.Interfaces
{
    public interface IUsuarioService
    {
        Task<Usuario> GetUserAsync(string email);
        Task<IdentityResult> AddUserAsync(Usuario user, string password);
        Task CheckRoleAsync(string roleName);
        Task AddUserToRoleAsync(Usuario user, string roleName);
        Task<bool> IsUserInRoleAsync(Usuario user, string roleName);
    }
}
