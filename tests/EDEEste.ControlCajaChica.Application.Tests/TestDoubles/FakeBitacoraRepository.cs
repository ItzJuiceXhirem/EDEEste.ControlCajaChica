using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    /// <summary>
    /// Bitácora en memoria. Guarda las mismas instancias que se le agregan, así una
    /// prueba puede "manipular" una fila (editarla o quitarla) igual que lo haría
    /// alguien con acceso directo a la base de datos. Respeta los lotes, para que la
    /// verificación de la cadena recorra los mismos cortes que contra SQL Server.
    /// </summary>
    public sealed class FakeBitacoraRepository : IBitacoraRepository
    {
        private readonly List<LogAuditoria> _logs = new();

        public void Agregar(IEnumerable<LogAuditoria> logs) => _logs.AddRange(logs);

        public void Quitar(LogAuditoria log) => _logs.Remove(log);

        public Task<IReadOnlyList<LogAuditoria>> ListarLoteEnOrdenAsync(
            long? despuesDeSecuencia,
            int tamano,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LogAuditoria>>(_logs
                .Where(l => despuesDeSecuencia is null || l.Secuencia > despuesDeSecuencia)
                .OrderBy(l => l.Secuencia)
                .Take(tamano)
                .ToList());

        public Task<IReadOnlyList<MencionEnBitacoraDto>> ListarUltimasMencionesAsync(
            IReadOnlyCollection<string> tablas,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MencionEnBitacoraDto>>(_logs
                .Where(l => tablas.Contains(l.NombreTabla))
                .GroupBy(l => (l.NombreTabla, l.RegistroId))
                .Select(g => g.MaxBy(l => l.Secuencia)!)
                .OrderBy(l => l.Secuencia)
                .Select(l => new MencionEnBitacoraDto
                {
                    NombreTabla = l.NombreTabla,
                    RegistroId = l.RegistroId,
                    Secuencia = l.Secuencia,
                    TipoAccion = l.TipoAccion,
                    FechaEjecucion = l.FechaEjecucion,
                    UsuarioId = l.UsuarioId
                })
                .ToList());

        // La consulta paginada y las opciones de filtro las usa solo la pantalla; ningún
        // handler las llama.
        public Task<Pagina<LogAuditoria>> ListarPaginaAsync(
            FiltroBitacora filtro,
            int numeroPagina,
            int tamanoPagina,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<OpcionesFiltroBitacoraDto> ListarOpcionesDeFiltroAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
