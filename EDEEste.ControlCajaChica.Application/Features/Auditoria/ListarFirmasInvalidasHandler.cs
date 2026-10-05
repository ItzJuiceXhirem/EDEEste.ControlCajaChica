using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Constants;

namespace EDEEste.ControlCajaChica.Application.Features.Auditoria
{
    /// <summary>
    /// Lista los registros cuya firma no coincide con sus datos. Detectarlos ya
    /// ocurría (IntegridadInterceptor los marca al leerlos y los handlers de dinero se
    /// niegan a operar con ellos); lo que faltaba era poder verlos juntos en un sitio.
    /// </summary>
    public sealed class ListarFirmasInvalidasHandler
    {
        private readonly IVerificadorIntegridadDatos _verificador;
        private readonly IAutorizacionService _autorizacion;

        public ListarFirmasInvalidasHandler(IVerificadorIntegridadDatos verificador, IAutorizacionService autorizacion)
        {
            _verificador = verificador;
            _autorizacion = autorizacion;
        }

        public async Task<ResultadoOperacion<IReadOnlyList<RegistroConFirmaInvalidaDto>>> EjecutarAsync(
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.ConsultarBitacora, cancellationToken))
            {
                return ResultadoOperacion<IReadOnlyList<RegistroConFirmaInvalidaDto>>.Fallo(
                    "No tiene permiso para revisar la integridad de los datos.");
            }

            return ResultadoOperacion<IReadOnlyList<RegistroConFirmaInvalidaDto>>.Ok(
                await _verificador.ListarFirmasInvalidasAsync(cancellationToken));
        }
    }
}
