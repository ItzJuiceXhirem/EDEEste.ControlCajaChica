using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    public sealed class IdentityService : IIdentityService
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IdentityOptions _opciones;

        public IdentityService(
            UserManager<Usuario> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> opciones)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _opciones = opciones.Value;
        }

        public bool RequiereCuentaConfirmada => _opciones.SignIn.RequireConfirmedAccount;

        public async Task AsegurarRolAsync(string rol)
        {
            if (!await _roleManager.RoleExistsAsync(rol))
            {
                await _roleManager.CreateAsync(new IdentityRole(rol));
            }
        }

        public async Task<ResultadoIdentidad> CrearUsuarioAsync(string email, string password, string nombre, string rol)
        {
            // Se acepta unicamente un rol del catalogo: asi un typo no termina creando
            // un rol nuevo y vacio al que despues nadie le aplica permisos.
            if (!RolesApp.Todos.Contains(rol))
            {
                return ResultadoIdentidad.Fallo($"El rol '{rol}' no existe en el catalogo de roles del sistema.");
            }

            var usuario = new Usuario
            {
                UserName = email,
                Email = email,
                Nombre = nombre,
                Activo = true
            };

            var resultado = await _userManager.CreateAsync(usuario, password);
            if (!resultado.Succeeded)
            {
                return ResultadoIdentidad.Fallo(resultado.Errors.Select(e => e.Description));
            }

            await AsegurarRolAsync(rol);
            var resultadoRol = await _userManager.AddToRoleAsync(usuario, rol);
            if (!resultadoRol.Succeeded)
            {
                // Sin rol la cuenta no sirve para nada y ademas queda invisible en el
                // control de accesos, asi que se revierte en vez de dejarla a medias.
                await _userManager.DeleteAsync(usuario);
                return ResultadoIdentidad.Fallo(resultadoRol.Errors.Select(e => e.Description));
            }

            return ResultadoIdentidad.Ok(usuario.Id);
        }

        public async Task<string?> GenerarTokenConfirmacionEmailAsync(string usuarioId)
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario is null)
            {
                return null;
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(usuario);

            // Base64Url sin relleno, igual que WebEncoders.Base64UrlEncode, que es lo
            // que espera ConfirmEmail.razor al decodificarlo.
            return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));
        }

        public async Task<string?> ObtenerNombreUsuarioAsync(string usuarioId)
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            return usuario?.UserName;
        }

        public async Task<bool> EstaEnRolAsync(string usuarioId, string rol)
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            return usuario is not null && await _userManager.IsInRoleAsync(usuario, rol);
        }
    }
}
