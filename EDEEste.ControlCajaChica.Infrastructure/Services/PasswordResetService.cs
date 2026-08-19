using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    public sealed class PasswordResetService : IPasswordResetService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Usuario> _userManager;

        public PasswordResetService(ApplicationDbContext context, UserManager<Usuario> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<string?> SolicitarAsync(string usuario)
        {
            var entidad = await _userManager.FindByNameAsync(usuario);
            if (entidad is null)
            {
                return null;
            }

            // Si ya hay una solicitud Pendiente de este mismo usuario, se reutiliza en
            // vez de crear otra: sin esto, alguien que reintenta unas cuantas veces le
            // llenaria la pantalla al Administrador de filas duplicadas para la misma
            // persona.
            var existente = await _context.SolicitudesPasswordReset
                .Where(s => s.UsuarioId == entidad.Id && s.Estado == EstadoSolicitudPasswordReset.Pendiente)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();

            if (existente != Guid.Empty)
            {
                return existente.ToString();
            }

            var solicitud = new SolicitudPasswordReset
            {
                UsuarioId = entidad.Id,
                FechaSolicitud = DateTime.UtcNow
            };

            _context.SolicitudesPasswordReset.Add(solicitud);
            await _context.SaveChangesAsync();

            return solicitud.Id.ToString();
        }

        public async Task<EstadoSolicitudPasswordReset?> ObtenerEstadoAsync(string solicitudId)
        {
            var solicitud = await ObtenerPorIdAsync(solicitudId);
            return solicitud?.Estado;
        }

        public async Task<IReadOnlyList<SolicitudPasswordResetResumenDto>> ListarPendientesAsync()
        {
            var pendientes = await _context.SolicitudesPasswordReset
                .Where(s => s.Estado == EstadoSolicitudPasswordReset.Pendiente)
                .OrderBy(s => s.FechaSolicitud)
                .ToListAsync();

            var resumen = new List<SolicitudPasswordResetResumenDto>(pendientes.Count);
            foreach (var solicitud in pendientes)
            {
                var usuario = await _userManager.FindByIdAsync(solicitud.UsuarioId);
                if (usuario is null)
                {
                    // La cuenta se borro despues de pedir el reseteo; no hay a quien
                    // devolverle acceso, asi que no tiene sentido mostrar la fila.
                    continue;
                }

                resumen.Add(new SolicitudPasswordResetResumenDto(
                    solicitud.Id.ToString(),
                    usuario.UserName ?? string.Empty,
                    usuario.Nombre,
                    solicitud.FechaSolicitud));
            }

            return resumen;
        }

        public async Task<ResultadoOperacion<string>> AceptarAsync(string solicitudId, string administradorId)
        {
            var solicitud = await ObtenerPorIdAsync(solicitudId);
            if (solicitud is null || solicitud.Estado != EstadoSolicitudPasswordReset.Pendiente)
            {
                return ResultadoOperacion<string>.Fallo("La solicitud no existe o ya fue resuelta.");
            }

            var usuario = await _userManager.FindByIdAsync(solicitud.UsuarioId);
            if (usuario is null)
            {
                return ResultadoOperacion<string>.Fallo("El usuario de esta solicitud ya no existe.");
            }

            // Se genera aqui y no al solicitar: el token de Identity vence por su
            // cuenta (1 dia por defecto), y ese plazo debe contar desde que se habilita
            // el cambio, no desde que se pidio, que pudo haber sido dias antes.
            solicitud.TokenReseteo = await _userManager.GeneratePasswordResetTokenAsync(usuario);
            solicitud.Estado = EstadoSolicitudPasswordReset.Aprobada;
            solicitud.FechaResolucion = DateTime.UtcNow;
            solicitud.ResueltaPorUsuarioId = administradorId;

            await _context.SaveChangesAsync();

            return ResultadoOperacion<string>.Ok(solicitud.Id.ToString());
        }

        public async Task<ResultadoOperacion<string>> IgnorarAsync(string solicitudId)
        {
            var solicitud = await ObtenerPorIdAsync(solicitudId);
            if (solicitud is null || solicitud.Estado != EstadoSolicitudPasswordReset.Pendiente)
            {
                return ResultadoOperacion<string>.Fallo("La solicitud no existe o ya fue resuelta.");
            }

            solicitud.Estado = EstadoSolicitudPasswordReset.Ignorada;
            solicitud.FechaResolucion = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return ResultadoOperacion<string>.Ok(solicitud.Id.ToString());
        }

        public async Task<bool> PuedeRestablecerAsync(string solicitudId)
        {
            var solicitud = await ObtenerPorIdAsync(solicitudId);
            return solicitud is not null && solicitud.Estado == EstadoSolicitudPasswordReset.Aprobada;
        }

        public async Task<ResultadoOperacion<string>> RestablecerAsync(string solicitudId, string nuevaPassword)
        {
            var solicitud = await ObtenerPorIdAsync(solicitudId);
            if (solicitud is null || solicitud.Estado != EstadoSolicitudPasswordReset.Aprobada || solicitud.TokenReseteo is null)
            {
                return ResultadoOperacion<string>.Fallo("El enlace no es valido o ya fue usado.");
            }

            var usuario = await _userManager.FindByIdAsync(solicitud.UsuarioId);
            if (usuario is null)
            {
                return ResultadoOperacion<string>.Fallo("El usuario de esta solicitud ya no existe.");
            }

            var resultado = await _userManager.ResetPasswordAsync(usuario, solicitud.TokenReseteo, nuevaPassword);
            if (!resultado.Succeeded)
            {
                // El token vencido cae aqui tambien (UserManager lo valida internamente),
                // asi que un enlace viejo se rechaza sin que la solicitud tenga que
                // rastrear su propio vencimiento por separado.
                return ResultadoOperacion<string>.Fallo(resultado.Errors.Select(e => e.Description));
            }

            // De un solo uso: una vez cambiada la contrasena, la misma URL no debe
            // volver a servir aunque el token de Identity todavia no haya vencido.
            solicitud.Estado = EstadoSolicitudPasswordReset.Usada;
            await _context.SaveChangesAsync();

            return ResultadoOperacion<string>.Ok(solicitud.Id.ToString());
        }

        private async Task<SolicitudPasswordReset?> ObtenerPorIdAsync(string solicitudId) =>
            Guid.TryParse(solicitudId, out var id)
                ? await _context.SolicitudesPasswordReset.FirstOrDefaultAsync(s => s.Id == id)
                : null;
    }
}
