using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Constants;

namespace EDEEste.ControlCajaChica.Application.Features.Auditoria
{
    /// <summary>
    /// Registros que la bitácora menciona pero que ya no existen en su tabla: la
    /// forma de notar un DELETE hecho directamente en la base de datos.
    ///
    /// Solo va en una dirección a propósito. Lo contrario (una fila sin rastro en la
    /// bitácora) no sirve como alarma: la bitácora se vació una vez (migración
    /// VaciarBitacoraPorFugaDeCamposSensibles), así que todo lo creado antes de esa
    /// fecha no tiene registro, y eso es lo esperado.
    ///
    /// Solo cuenta las tablas que IVerificadorIntegridadDatos declara verificables
    /// (las de borrado lógico). Un RegistroId que no es un Guid no puede ser la clave
    /// de ninguna de ellas y se ignora.
    /// </summary>
    public sealed class ListarRegistrosDesaparecidosHandler
    {
        private readonly IBitacoraRepository _bitacora;
        private readonly IVerificadorIntegridadDatos _verificador;
        private readonly IAutorizacionService _autorizacion;

        public ListarRegistrosDesaparecidosHandler(
            IBitacoraRepository bitacora,
            IVerificadorIntegridadDatos verificador,
            IAutorizacionService autorizacion)
        {
            _bitacora = bitacora;
            _verificador = verificador;
            _autorizacion = autorizacion;
        }

        public async Task<ResultadoOperacion<IReadOnlyList<MencionEnBitacoraDto>>> EjecutarAsync(
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.ConsultarBitacora, cancellationToken))
            {
                return ResultadoOperacion<IReadOnlyList<MencionEnBitacoraDto>>.Fallo(
                    "No tiene permiso para revisar la integridad de los datos.");
            }

            var existentes = await _verificador.ObtenerIdsExistentesAsync(cancellationToken);
            var menciones = await _bitacora.ListarUltimasMencionesAsync(existentes.Keys.ToList(), cancellationToken);

            IReadOnlyList<MencionEnBitacoraDto> desaparecidos = menciones
                .Where(mencion => existentes.TryGetValue(mencion.NombreTabla, out var ids)
                                  && Guid.TryParse(mencion.RegistroId, out var id)
                                  && !ids.Contains(id))
                .OrderBy(mencion => mencion.Secuencia)
                .ToList();

            return ResultadoOperacion<IReadOnlyList<MencionEnBitacoraDto>>.Ok(desaparecidos);
        }
    }
}
