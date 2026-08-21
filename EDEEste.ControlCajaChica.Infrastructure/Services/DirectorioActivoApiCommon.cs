using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// Consulta el directorio activo de la empresa a traves del APICommon.
    ///
    /// Esta parte SI esta completa: el contrato de GetUserByUserName se conoce y esta
    /// implementado. Lo que falta para la V1 es la validacion de credenciales, que
    /// vive en <see cref="AutenticacionActiveDirectory"/>.
    /// </summary>
    public sealed class DirectorioActivoApiCommon : IDirectorioActivoService
    {
        /// <summary>
        /// El APICommon responde en camelCase; en vez de ensuciar los DTO de
        /// Application con atributos de serializacion se ignora el caso al leer.
        /// </summary>
        internal static readonly JsonSerializerOptions OpcionesJson = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _http;
        private readonly ILogger<DirectorioActivoApiCommon> _logger;

        public DirectorioActivoApiCommon(HttpClient http, ILogger<DirectorioActivoApiCommon> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<UsuarioDirectorioDto?> ObtenerUsuarioAsync(
            string userName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            var ruta = $"{OpcionesApiCommon.RutaObtenerUsuario}?userName={Uri.EscapeDataString(userName)}";

            var respuesta = await _http.GetAsync(ruta, cancellationToken);

            // Que no exista la persona es una respuesta normal, no una falla: se
            // distingue del resto de errores para no registrarlo como problema.
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "El APICommon respondio {Codigo} al consultar el usuario {Usuario}.",
                    (int)respuesta.StatusCode, userName);
                return null;
            }

            var contenido = await respuesta.Content
                .ReadFromJsonAsync<RespuestaApiCommon<UsuarioDirectorioDto>>(OpcionesJson, cancellationToken);

            // El payload util siempre viene dentro de "data"; si llega vacio se trata
            // igual que un usuario inexistente.
            return contenido?.Data;
        }
    }
}
