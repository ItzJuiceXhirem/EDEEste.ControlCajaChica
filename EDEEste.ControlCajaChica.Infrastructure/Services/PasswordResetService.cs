using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        // 12h y no las 24h que vencen por defecto los tokens de Identity: es una
        // segunda ventana, mas corta y bajo control nuestro, sobre el mismo enlace.
        private static readonly TimeSpan VigenciaSecreto = TimeSpan.FromHours(12);

        // 256 bits: mismo tamano que exige la clave HMAC de la aplicacion. No hace
        // falta mas para un secreto de un solo uso que ademas expira.
        private const int BytesSecreto = 32;

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

        public async Task<ResultadoOperacion<EnlaceRestablecimientoDto>> AceptarAsync(string solicitudId, string administradorId)
        {
            var solicitud = await ObtenerPorIdAsync(solicitudId);
            if (solicitud is null || solicitud.Estado != EstadoSolicitudPasswordReset.Pendiente)
            {
                return ResultadoOperacion<EnlaceRestablecimientoDto>.Fallo("La solicitud no existe o ya fue resuelta.");
            }

            var usuario = await _userManager.FindByIdAsync(solicitud.UsuarioId);
            if (usuario is null)
            {
                return ResultadoOperacion<EnlaceRestablecimientoDto>.Fallo("El usuario de esta solicitud ya no existe.");
            }

            // Se genera aqui y no al solicitar: el token de Identity vence por su
            // cuenta (1 dia por defecto), y ese plazo debe contar desde que se habilita
            // el cambio, no desde que se pidio, que pudo haber sido dias antes.
            solicitud.TokenReseteo = await _userManager.GeneratePasswordResetTokenAsync(usuario);

            // El secreto en si NUNCA se guarda -- solo su hash. Quien pidio el
            // restablecimiento ya conoce el Id de la solicitud (se lo devuelve
            // SolicitarAsync), asi que sin este segundo dato, conocer el Id no le
            // sirve de nada: el enlace real solo lo tiene quien recibe este resultado.
            var secreto = CodificarBase64Url(RandomNumberGenerator.GetBytes(BytesSecreto));
            solicitud.HashSecreto = CalcularHashSecreto(secreto);
            solicitud.FechaExpiracionSecreto = DateTime.UtcNow.Add(VigenciaSecreto);

            solicitud.Estado = EstadoSolicitudPasswordReset.Aprobada;
            solicitud.FechaResolucion = DateTime.UtcNow;
            solicitud.ResueltaPorUsuarioId = administradorId;

            await _context.SaveChangesAsync();

            return ResultadoOperacion<EnlaceRestablecimientoDto>.Ok(new EnlaceRestablecimientoDto(solicitud.Id.ToString(), secreto));
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

        public async Task<bool> PuedeRestablecerAsync(string solicitudId, string secreto)
        {
            var solicitud = await ObtenerPorIdAsync(solicitudId);
            return EnlaceEsValido(solicitud, secreto);
        }

        public async Task<ResultadoOperacion<string>> RestablecerAsync(string solicitudId, string secreto, string nuevaPassword)
        {
            var solicitud = await ObtenerPorIdAsync(solicitudId);
            if (!EnlaceEsValido(solicitud, secreto))
            {
                return ResultadoOperacion<string>.Fallo("El enlace no es válido, ya fue usado o expiró.");
            }

            var usuario = await _userManager.FindByIdAsync(solicitud!.UsuarioId);
            if (usuario is null)
            {
                return ResultadoOperacion<string>.Fallo("El usuario de esta solicitud ya no existe.");
            }

            var resultado = await _userManager.ResetPasswordAsync(usuario, solicitud.TokenReseteo!, nuevaPassword);
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

        /// <summary>
        /// Tres condiciones, todas obligatorias: Aprobada, dentro de la ventana de
        /// <see cref="VigenciaSecreto"/>, y el secreto recibido coincide con el hash
        /// guardado. Sin el secreto correcto, conocer el Id de la solicitud (que el
        /// solicitante SI conoce) no alcanza para nada.
        /// </summary>
        private static bool EnlaceEsValido(SolicitudPasswordReset? solicitud, string secreto)
        {
            if (solicitud is null
                || solicitud.Estado != EstadoSolicitudPasswordReset.Aprobada
                || solicitud.TokenReseteo is null
                || string.IsNullOrEmpty(solicitud.HashSecreto))
            {
                return false;
            }

            if (solicitud.FechaExpiracionSecreto is null || solicitud.FechaExpiracionSecreto < DateTime.UtcNow)
            {
                return false;
            }

            if (string.IsNullOrEmpty(secreto))
            {
                return false;
            }

            // Comparacion en tiempo constante: el secreto decide si alguien puede
            // cambiar una contrasena ajena, asi que se trata como cualquier otro
            // secreto criptografico de la aplicacion.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(CalcularHashSecreto(secreto)),
                Encoding.UTF8.GetBytes(solicitud.HashSecreto));
        }

        private static string CalcularHashSecreto(string secreto) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secreto)));

        /// <summary>
        /// Base64Url a mano (sin depender de Microsoft.AspNetCore.WebUtilities, que
        /// Infrastructure no referencia): mismo alfabeto que un token en una URL sin
        /// necesitar escapar '+', '/' ni el relleno '='.
        /// </summary>
        private static string CodificarBase64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private async Task<SolicitudPasswordReset?> ObtenerPorIdAsync(string solicitudId) =>
            Guid.TryParse(solicitudId, out var id)
                ? await _context.SolicitudesPasswordReset.FirstOrDefaultAsync(s => s.Id == id)
                : null;
    }
}
