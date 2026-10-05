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
            /* Se acepta únicamente un rol del catálogo: así un typo no termina creando
               un rol nuevo y vacío al que después nadie le aplica permisos. */
            if (!RolesApp.Todos.Contains(rol))
            {
                return ResultadoIdentidad.Fallo($"El rol '{rol}' no existe en el catálogo de roles del sistema.");
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
                /* Sin rol la cuenta no sirve para nada y además queda invisible en el
                   control de accesos, así que se revierte en vez de dejarla a medias. */
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
                return ResultadoIdentidad.Fallo($"El rol '{rol}' no existe en el catálogo de roles del sistema.");
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

            /* El rol se conserva: si más adelante se le devuelve el acceso, el
               Administrador ve que rol tenía antes en vez de tener que adivinarlo. */
            usuario.EstadoAcceso = EstadoAccesoUsuario.Denegado;
            return await GuardarYRefrescarSelloAsync(usuario);
        }

        public async Task<ResultadoIdentidad> CambiarRolAsync(string usuarioId, string nuevoRol)
        {
            if (!RolesApp.Todos.Contains(nuevoRol))
            {
                return ResultadoIdentidad.Fallo($"El rol '{nuevoRol}' no existe en el catálogo de roles del sistema.");
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
        /// acaban de denegarle el acceso seguiría navegando con la cookie que ya
        /// tenía: el proveedor de estado revalida ese sello, y solo cambiándolo se
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

            /* Una consulta de roles por usuario. Es N+1, pero la plantilla de una caja
               chica son decenas de cuentas, no miles, y aplanarlo con un join manual
               contra AspNetUserRoles ataría esta clase al esquema de Identity. */
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
                    usuario.PhoneNumber,
                    !string.IsNullOrWhiteSpace(usuario.RutaFotoPerfil)));
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

        public async Task<IReadOnlyDictionary<string, string>> ObtenerNombresUsuarioAsync(IEnumerable<string> usuarioIds)
        {
            var ids = usuarioIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            return await _userManager.Users
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Id);
        }

        public async Task<bool> EstaEnRolAsync(string usuarioId, string rol)
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId);
            return usuario is not null && await _userManager.IsInRoleAsync(usuario, rol);
        }

        public async Task<DateTime?> RegistrarAccesoAsync(string usuarioId)
        {
            /* Proyección, no ObtenerPorIdAsync: no hace falta materializar (ni
               rastrear) la entidad completa solo para leer una columna. */
            var anterior = await _userManager.Users
                .Where(u => u.Id == usuarioId)
                .Select(u => u.UltimoAccesoUtc)
                .FirstOrDefaultAsync();

            /* ExecuteUpdateAsync arma un UPDATE dirigido a esta columna y lo manda
               directo a SQL Server -- nunca pasa por SaveChanges, por lo tanto nunca
               por el ChangeTracker ni por AuditoriaInterceptor. Es una garantía
               estructural (no una lista negra que alguien tiene que recordar
               mantener) de que este toque a AspNetUsers, que ocurre en CADA login del
               sistema, jamás puede terminar copiando PasswordHash/SecurityStamp a
               LogsAuditoria. */
            await _userManager.Users
                .Where(u => u.Id == usuarioId)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(u => u.UltimoAccesoUtc, DateTime.UtcNow));

            return anterior;
        }

        public Task<string?> ObtenerRutaFotoPerfilAsync(string usuarioId) =>
            _userManager.Users
                .Where(u => u.Id == usuarioId)
                .Select(u => u.RutaFotoPerfil)
                .FirstOrDefaultAsync();

        /* Mismo razonamiento que RegistrarAccesoAsync: UPDATE dirigido a una sola
           columna, sin ChangeTracker ni AuditoriaInterceptor de por medio. Cambiar la
           foto de perfil no tiene por que arrastrar el resto de la fila del usuario
           (PasswordHash incluido) ni generar una entrada de bitácora. */
        public Task ActualizarFotoPerfilAsync(string usuarioId, string? rutaRelativa) =>
            _userManager.Users
                .Where(u => u.Id == usuarioId)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(u => u.RutaFotoPerfil, rutaRelativa));

        public Task<string?> ObtenerTemaPreferidoAsync(string usuarioId) =>
            _userManager.Users
                .Where(u => u.Id == usuarioId)
                .Select(u => u.TemaPreferido)
                .FirstOrDefaultAsync();

        public Task ActualizarTemaPreferidoAsync(string usuarioId, string? tema) =>
            _userManager.Users
                .Where(u => u.Id == usuarioId)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(u => u.TemaPreferido, tema));
    }
}
