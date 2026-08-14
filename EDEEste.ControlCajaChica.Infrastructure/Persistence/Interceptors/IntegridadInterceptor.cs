using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Interceptors
{
    /// <summary>
    /// Lado de lectura del sello de inmutabilidad: cada vez que EF materializa una
    /// entidad firmada se recalcula el HMAC con la clave del servidor y se compara
    /// con el que trae la fila.
    ///
    /// Si un DBA cambia un monto directamente en la BDD, no puede recalcular la
    /// firma (no tiene la clave), asi que el hash guardado deja de corresponder y
    /// la entidad llega a la aplicacion marcada como comprometida.
    ///
    /// Aqui no se lanza excepcion a proposito: se marca y se registra. Reventar en
    /// plena lectura dejaria al auditor sin poder ni siquiera listar los registros
    /// alterados, que es justo lo que necesita ver. El bloqueo real ocurre al
    /// intentar guardar, en <see cref="AuditoriaInterceptor"/>.
    /// </summary>
    public sealed class IntegridadInterceptor : IMaterializationInterceptor
    {
        private readonly ICriptografiaService _criptografiaService;
        private readonly ILogger<IntegridadInterceptor> _logger;

        public IntegridadInterceptor(
            ICriptografiaService criptografiaService,
            ILogger<IntegridadInterceptor> logger)
        {
            _criptografiaService = criptografiaService;
            _logger = logger;
        }

        public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
        {
            if (instance is not ITamperProofEntity entidad)
            {
                return instance;
            }

            if (string.IsNullOrEmpty(entidad.HashFirma))
            {
                // Fila anterior a la implementacion del sello, o insertada por fuera
                // de la aplicacion. No se puede afirmar que sea integra.
                entidad.IntegridadVerificada = false;
                _logger.LogWarning(
                    "El registro de {Entidad} no tiene firma de integridad almacenada.",
                    instance.GetType().Name);
                return instance;
            }

            entidad.IntegridadVerificada = _criptografiaService.ValidarIntegridad(entidad);

            if (!entidad.IntegridadVerificada)
            {
                _logger.LogCritical(
                    "ALERTA DE MANIPULACION: el registro de {Entidad} fue alterado directamente " +
                    "en la base de datos sin autorizacion del programa.",
                    instance.GetType().Name);
            }

            return instance;
        }
    }
}
