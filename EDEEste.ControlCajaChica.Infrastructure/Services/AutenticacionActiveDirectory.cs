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
    /// V1: las credenciales las valida el Active Directory de la empresa a traves del
    /// APICommon. <b>Incompleta a proposito.</b>
    ///
    /// Se deja como codigo real (compila, se revisa, se registra en DI) en vez de
    /// como comentarios, porque el codigo comentado no compila, nadie lo revisa y se
    /// pudre. Solo no esta seleccionada: <c>Autenticacion:Modo</c> vale Local por
    /// defecto, y ponerla en ActiveDirectory sin API Key impide arrancar la
    /// aplicacion (ver DependencyInjection.AgregarAutenticacion).
    ///
    /// Lo que falta es unicamente <see cref="ValidarAsync"/>. Cuando se tenga la API
    /// Key y el contrato de <c>ValidateCredentials</c>, el trabajo pendiente es:
    ///
    /// 1. Hacer POST a <see cref="OpcionesApiCommon.RutaValidarCredenciales"/> con el
    ///    cuerpo que ese endpoint espere (probablemente { userName, password }).
    /// 2. Traducir su respuesta a Ok/Fallo.
    /// 3. En caso de exito, traer la ficha con <see cref="IDirectorioActivoService"/>
    ///    y devolverla en el resultado, para que el login pueda crear o refrescar el
    ///    registro local del usuario con su nombre y departamento reales.
    ///
    /// Queda pendiente ademas una decision de producto que no es tecnica: que hacer
    /// cuando alguien valida bien contra el AD pero todavia no tiene cuenta local.
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
                "La autenticacion contra Active Directory todavia no esta implementada. " +
                $"Se conoce la ruta ({OpcionesApiCommon.RutaValidarCredenciales}) pero no que " +
                "recibe ni que devuelve ese endpoint, y falta la API Key del APICommon. " +
                "Mientras tanto use Autenticacion:Modo = Local.");
    }
}
