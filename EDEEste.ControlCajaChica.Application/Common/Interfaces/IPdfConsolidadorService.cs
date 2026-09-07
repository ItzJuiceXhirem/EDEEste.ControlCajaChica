using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Genera el expediente PDF consolidado de una solicitud de reposicion: un
    /// resumen de los gastos incluidos, seguido de cada comprobante adjunto con una
    /// portada de transcripcion antes de su contenido.
    ///
    /// Trabaja sobre las entidades de Domain directo, no sobre DTOs. Quien llame debe
    /// haber cargado solicitud.Gastos y cada gasto.Comprobantes de antemano
    /// (Include/ThenInclude); este servicio no consulta la base de datos.
    /// </summary>
    public interface IPdfConsolidadorService
    {
        Task<byte[]> ConsolidarComprobantesAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default);
    }
}
