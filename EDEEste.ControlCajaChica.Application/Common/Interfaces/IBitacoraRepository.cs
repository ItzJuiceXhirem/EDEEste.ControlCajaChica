using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Lectura de la bitácora de auditoría. Solo lee: la bitácora la escribe
    /// únicamente AuditoriaInterceptor, dentro del mismo guardado que la origina, y
    /// nada en la aplicación debe poder agregarle, cambiarle ni quitarle filas.
    /// </summary>
    public interface IBitacoraRepository
    {
        // Una página del resultado filtrado, de la entrada más reciente a la más antigua
        Task<Pagina<LogAuditoria>> ListarPaginaAsync(
            FiltroBitacora filtro,
            int numeroPagina,
            int tamanoPagina,
            CancellationToken cancellationToken = default);

      /* Un lote de la cadena en su orden (Secuencia ascendente), a partir de la fila
         siguiente a despuesDeSecuencia, o desde el principio si es null. Se recorre
         por lotes y no con un lector abierto: en Blazor Server el DbContext vive todo
         el circuito, y un lector largo bloquearía cualquier otra consulta mientras
         dura la verificación. */
        Task<IReadOnlyList<LogAuditoria>> ListarLoteEnOrdenAsync(
            long? despuesDeSecuencia,
            int tamano,
            CancellationToken cancellationToken = default);

        Task<OpcionesFiltroBitacoraDto> ListarOpcionesDeFiltroAsync(CancellationToken cancellationToken = default);

        // Por cada registro de esas tablas, la última vez que la bitácora lo menciona
        Task<IReadOnlyList<MencionEnBitacoraDto>> ListarUltimasMencionesAsync(
            IReadOnlyCollection<string> tablas,
            CancellationToken cancellationToken = default);
    }
}
