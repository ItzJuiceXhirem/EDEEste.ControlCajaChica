using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Aprueba o rechaza una solicitud de reposicion.
    ///
    /// No inyecta IFondoRepository a proposito: ni aprobar ni rechazar mueven dinero,
    /// y no tener el repositorio disponible lo hace evidente en la firma del
    /// constructor. El efectivo solo vuelve al fondo cuando Finanzas paga
    /// (ProcesarPagoReposicionHandler).
    /// </summary>
    public sealed class AprobarReposicionHandler
    {
        private readonly IReposicionRepository _reposiciones;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IApplicationDbContext _contexto;

        public AprobarReposicionHandler(
            IReposicionRepository reposiciones,
            ICurrentUserService usuarioActual,
            IApplicationDbContext contexto)
        {
            _reposiciones = reposiciones;
            _usuarioActual = usuarioActual;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            AprobarReposicionCommand comando,
            CancellationToken cancellationToken = default)
        {
            var solicitud = await _reposiciones.ObtenerConDetalleAsync(comando.ReposicionId, cancellationToken);
            if (solicitud is null)
            {
                return ResultadoOperacion<Guid>.Fallo("La solicitud indicada no existe.");
            }

            var errores = Validar(solicitud);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);

            solicitud.GerenteUsuarioId = usuario.Id ?? string.Empty;
            solicitud.FechaAprobacion = DateTime.UtcNow;

            if (comando.Aprobar)
            {
                solicitud.Estado = EstadoReposicion.Aprobada;
            }
            else
            {
                solicitud.Estado = EstadoReposicion.Rechazada;

                // Los gastos vuelven al ruedo para que el custodio corrija y arme otra
                // solicitud. El balance NO se toca: el efectivo nunca volvio a la
                // caja, se sigue debiendo. ToList() no es cosmetico: al poner
                // ReposicionId en null, el arreglo de relaciones de EF saca el gasto
                // de solicitud.Gastos en plena iteracion.
                foreach (var gasto in solicitud.Gastos.ToList())
                {
                    gasto.ReposicionId = null;
                    gasto.Estado = EstadoGasto.PendienteReposicion;
                }
            }

            // RutaPdfConsolidado se conserva: una rechazada queda en el historial con
            // su expediente.
            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modifico esta solicitud mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(solicitud.Id);
        }

        private static List<string> Validar(SolicitudReposicion solicitud)
        {
            var errores = new List<string>();

            if (solicitud.Estado != EstadoReposicion.PendienteAprobacion)
            {
                errores.Add(
                    $"Solo se puede aprobar o rechazar una solicitud pendiente de aprobacion. " +
                    $"Esta solicitud esta en estado {solicitud.Estado}.");
            }

            if (!solicitud.IntegridadVerificada)
            {
                errores.Add("La solicitud tiene la firma de integridad comprometida y no se puede procesar.");
            }

            foreach (var gasto in solicitud.Gastos)
            {
                if (gasto.Estado != EstadoGasto.EnProcesoReposicion)
                {
                    errores.Add($"El gasto {gasto.NCF} no esta en proceso de reposicion; la solicitud esta inconsistente.");
                }

                if (!gasto.IntegridadVerificada)
                {
                    errores.Add($"El gasto {gasto.NCF} tiene la firma de integridad comprometida.");
                }
            }

            return errores;
        }
    }
}
