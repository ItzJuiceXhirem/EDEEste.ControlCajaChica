using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IReposicionRepository
    {
        // Trae la solicitud con sus gastos y los comprobantes de cada uno
        Task<SolicitudReposicion?> ObtenerConDetalleAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SolicitudReposicion>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SolicitudReposicion>> ListarAsync(CancellationToken cancellationToken = default);

      /* Solicitudes en un estado dado, de todos los fondos. Es la bandeja del
         Gerente (pendientes de aprobación) y la de Finanzas (aprobadas por pagar):
         ninguno de los dos trabaja por fondo, trabajan por cola. */
        Task<IReadOnlyList<SolicitudReposicion>> ListarPorEstadoAsync(EstadoReposicion estado, CancellationToken cancellationToken = default);

      /* Si el fondo tiene una solicitud viva (ver ReglasEstado.ReposicionEnCurso).
         Un fondo admite como máximo una: es lo que garantiza que su estado, Activo o
         EnReposicion, siempre es el correcto. */
        Task<bool> ExisteSolicitudEnCursoAsync(Guid fondoId, CancellationToken cancellationToken = default);

      /* Las solicitudes vivas del fondo, con sus gastos. Las necesita el cierre del
         fondo, que las rechaza y devuelve esos gastos a pendientes. */
        Task<IReadOnlyList<SolicitudReposicion>> ListarEnCursoPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default);

        Task AgregarAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default);
    }
}
