using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// V1: las credenciales las valida el Active Directory de la empresa a través del
    /// APICommon. <b>Incompleta a propósito.</b>
    ///
    /// Se deja como código real (compila, se revisa, se registra en DI) en vez de
    /// como comentarios, porque el código comentado no compila, nadie lo revisa y se
    /// pudre. Solo no está seleccionada: <c>Autenticacion:Modo</c> vale Local por
    /// defecto, y ponerla en ActiveDirectory sin API Key impide arrancar la
    /// aplicación (ver DependencyInjection.AgregarAutenticacion).
    ///
    /// Lo que falta es únicamente <see cref="ValidarAsync"/>. Cuando se tenga la API
    /// Key y el contrato de <c>ValidateCredentials</c>, el trabajo pendiente es:
    ///
    /// 1. Hacer POST a <see cref="OpcionesApiCommon.RutaValidarCredenciales"/> con el
    ///    cuerpo que ese endpoint espere (probablemente { userName, password }).
    /// 2. Traducir su respuesta a Ok/Fallo.
    /// 3. En caso de éxito, traer la ficha con <see cref="IDirectorioActivoService"/>
    ///    y devolverla en el resultado, para que el login pueda crear o refrescar el
    ///    registro local del usuario con su nombre y departamento reales.
    ///
    /// Queda pendiente además una decisión de producto que no es técnica: que hacer
    /// cuando alguien valida bien contra el AD pero todavía no tiene cuenta local.
    /// Lo coherente con el modelo actual es crearla en estado Pendiente para que un
    /// Administrador le asigne rol, pero hay que confirmarlo.
    /// </summary>
    public sealed class AutenticacionActiveDirectory : IAutenticadorCredenciales
    {
        private readonly HttpClient _http;
        private readonly IDirectorioActivoService _directorio;

        public AutenticacionActiveDirectory(HttpClient http, IDirectorioActivoService directorio)
        {
            _http = http;
            _directorio = directorio;
        }

        public Task<ResultadoAutenticacion> ValidarAsync(
            string usuario,
            string password,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "La autenticación contra Active Directory todavía no está implementada. " +
                $"Se conoce la ruta ({OpcionesApiCommon.RutaValidarCredenciales}) pero no qué " +
                "recibe ni que devuelve ese endpoint, y falta la API Key del APICommon. " +
                "Mientras tanto use Autenticacion:Modo = Local.");
    }
}
