using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    /// <summary>
    /// Guarda las mismas instancias que se le agregan, no copias: es justo el
    /// comportamiento que el ChangeTracker de EF Core le da a un handler real (el
    /// objeto que el handler muta es el mismo que la prueba puede inspeccionar
    /// despues), y es lo que hace inutil un mock generico aqui.
    /// </summary>
    public sealed class FakeReposicionRepository : IReposicionRepository
    {
        private readonly Dictionary<Guid, SolicitudReposicion> _solicitudes = new();

        public void Agregar(SolicitudReposicion solicitud) => _solicitudes[solicitud.Id] = solicitud;

        public Task<SolicitudReposicion?> ObtenerConDetalleAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_solicitudes.GetValueOrDefault(id));

        public Task<IReadOnlyList<SolicitudReposicion>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SolicitudReposicion>>(
                _solicitudes.Values.Where(s => s.FondoCajaChicaId == fondoId).ToList());

        public Task<IReadOnlyList<SolicitudReposicion>> ListarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SolicitudReposicion>>(_solicitudes.Values.ToList());

        public Task<IReadOnlyList<SolicitudReposicion>> ListarPorEstadoAsync(EstadoReposicion estado, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SolicitudReposicion>>(
                _solicitudes.Values.Where(s => s.Estado == estado).ToList());

        public Task AgregarAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default)
        {
            Agregar(solicitud);
            return Task.CompletedTask;
        }
    }
}
