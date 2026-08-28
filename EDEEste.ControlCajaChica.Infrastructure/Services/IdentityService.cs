using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    public sealed class IdentityService : IIdentityService
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public IdentityService(
            UserManager<Usuario> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task AsegurarRolAsync(string rol)
        {
            if (!await _roleManager.RoleExistsAsync(rol))
            {
                await _roleManager.CreateAsync(new IdentityRole(rol));
            }
        }

        public async Task<ResultadoIdentidad> CrearUsuarioAsync(string usuario, string password, string nombre, string rol)
        {
            // Se acepta unicamente un rol del catalogo: asi un typo no termina creando
            // un rol nuevo y vacio al que despues nadie le aplica permisos.
            if (!RolesApp.Todos.Contains(rol))
            {
                return ResultadoIdentidad.Fallo($"El rol '{rol}' no existe en el catalogo de roles del sistema.");
            }

            var creado = await CrearAsync(usuario, password, nombre, EstadoAccesoUsuario.Aprobado);
            if (!creado.Exitoso)
            {
                return creado;
            }

            var entidad = await _userManager.FindByIdAsync(creado.UsuarioId!);
            await AsegurarRolAsync(rol);
            var resultadoRol = await _userManager.AddToRoleAsync(entidad!, rol);
            if (!resultadoRol.Succeeded)
            {
                // Sin rol la cuenta no sirve para nada y ademas queda invisible en el
                // control de accesos, asi que se revierte en vez de dejarla a medias.
                await _userManager.DeleteAsync(entidad!);
                return ResultadoIdentidad.Fallo(resultadoRol.Errors.Select(e => e.Description));
            }

            return creado;
        }

        public Task<ResultadoIdentidad> CrearUsuarioPendienteAsync(string usuario, string password, string nombre) =>
            CrearAsync(usuario, password, nombre, EstadoAccesoUsuario.Pendiente);

        private async Task<ResultadoIdentidad> CrearAsync(
            string usuario, string password, string nombre, EstadoAccesoUsuario estado)
        {
            var entidad = new Usuario
            {
                UserName = usuario,
                Nombre = nombre,
                EstadoAcceso = estado
            };

            var resultado = await _userManager.CreateAsync(entidad, password);

            return resultado.Succeeded
                ? ResultadoIdentidad.Ok(entidad.Id)
                : ResultadoIdentidad.Fallo(resultado.Errors.Select(e => e.Description));
        }

        public async Task<EstadoAccesoUsuario?> ObtenerEstadoAccesoAsync(string usuarioId)
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            return usuario?.EstadoAcceso;
        }

        public async Task<ResultadoIdentidad> AprobarAccesoAsync(string usuarioId, string rol)
        {
            if (!RolesApp.Todos.Contains(rol))
            {
                return ResultadoIdentidad.Fallo($"El rol '{rol}' no existe en el catalogo de roles del sistema.");
            }

            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario is null)
            {
                return ResultadoIdentidad.Fallo("El usuario indicado no existe.");
            }

            var cambio = await ReemplazarRolAsync(usuario, rol);
            if (!cambio.Exitoso)
            {
                return cambio;
            }

            usuario.EstadoAcceso = EstadoAccesoUsuario.Aprobado;
            return await GuardarYRefrescarSelloAsync(usuario);
        }

        public async Task<ResultadoIdentidad> DenegarAccesoAsync(string usuarioId)
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario is null)
            {
                return ResultadoIdentidad.Fallo("El usuario indicado no existe.");
            }

            // El rol se conserva: si mas adelante se le devuelve el acceso, el
            // Administrador ve que rol tenia antes en vez de tener que adivinarlo.
            usuario.EstadoAcceso = EstadoAccesoUsuario.Denegado;
            return await GuardarYRefrescarSelloAsync(usuario);
        }

        public async Task<ResultadoIdentidad> CambiarRolAsync(string usuarioId, string nuevoRol)
        {
            if (!RolesApp.Todos.Contains(nuevoRol))
            {
                return ResultadoIdentidad.Fallo($"El rol '{nuevoRol}' no existe en el catalogo de roles del sistema.");
            }

            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario is null)
            {
                return ResultadoIdentidad.Fallo("El usuario indicado no existe.");
            }

            var cambio = await ReemplazarRolAsync(usuario, nuevoRol);
            return cambio.Exitoso
                ? await GuardarYRefrescarSelloAsync(usuario)
                : cambio;
        }

        private async Task<ResultadoIdentidad> ReemplazarRolAsync(Usuario usuario, string rol)
        {
            var actuales = await _userManager.GetRolesAsync(usuario);
            if (actuales.Count > 0)
            {
                var quitados = await _userManager.RemoveFromRolesAsync(usuario, actuales);
                if (!quitados.Succeeded)
                {
                    return ResultadoIdentidad.Fallo(quitados.Errors.Select(e => e.Description));
                }
            }

            await AsegurarRolAsync(rol);
            var agregado = await _userManager.AddToRoleAsync(usuario, rol);

            return agregado.Succeeded
                ? ResultadoIdentidad.Ok(usuario.Id)
                : ResultadoIdentidad.Fallo(agregado.Errors.Select(e => e.Description));
        }

        /// <summary>
        /// Guarda y renueva el sello de seguridad. Sin renovarlo, alguien a quien
        /// acaban de denegarle el acceso seguiria navegando con la cookie que ya
        /// tenia: el proveedor de estado revalida ese sello, y solo cambiandolo se
        /// invalidan las sesiones abiertas de ese usuario.
        /// </summary>
        private async Task<ResultadoIdentidad> GuardarYRefrescarSelloAsync(Usuario usuario)
        {
            var actualizado = await _userManager.UpdateAsync(usuario);
            if (!actualizado.Succeeded)
            {
                return ResultadoIdentidad.Fallo(actualizado.Errors.Select(e => e.Description));
            }

            await _userManager.UpdateSecurityStampAsync(usuario);
            return ResultadoIdentidad.Ok(usuario.Id);
        }

        public async Task<IReadOnlyList<UsuarioResumenDto>> ListarUsuariosAsync(EstadoAccesoUsuario? estado = null)
        {
            var consulta = _userManager.Users.AsNoTracking();
            if (estado is not null)
            {
                consulta = consulta.Where(u => u.EstadoAcceso == estado);
            }

            var usuarios = await consulta.OrderBy(u => u.UserName).ToListAsync();

            // Una consulta de roles por usuario. Es N+1, pero la plantilla de una caja
            // chica son decenas de cuentas, no miles, y aplanarlo con un join manual
            // contra AspNetUserRoles ataria esta clase al esquema de Identity.
            var resumen = new List<UsuarioResumenDto>(usuarios.Count);
            foreach (var usuario in usuarios)
            {
                var roles = await _userManager.GetRolesAsync(usuario);
                resumen.Add(new UsuarioResumenDto(
                    usuario.Id,
                    usuario.UserName ?? string.Empty,
                    usuario.Nombre,
                    roles.FirstOrDefault(),
                    usuario.EstadoAcceso,
                    usuario.FechaCreacion,
                    usuario.PhoneNumber));
            }

            return resumen;
        }

        public async Task<bool> ExisteAdministradorAsync()
        {
            var administradores = await _userManager.GetUsersInRoleAsync(RolesApp.Administrador);
            return administradores.Count > 0;
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
