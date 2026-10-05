using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EDEEste.ControlCajaChica.Infrastructure.Repositories
{
    /// <summary>
    /// Todas las consultas son AsNoTracking: la bitácora solo se lee, y en Blazor
    /// Server el DbContext vive todo el circuito -- rastrear miles de filas de
    /// bitácora las dejaría en memoria hasta que el usuario cierre la pestaña.
    /// </summary>
    public sealed class BitacoraRepository : IBitacoraRepository
    {
        private readonly ApplicationDbContext _context;

        public BitacoraRepository(ApplicationDbContext context) => _context = context;

        public async Task<Pagina<LogAuditoria>> ListarPaginaAsync(
            FiltroBitacora filtro,
            int numeroPagina,
            int tamanoPagina,
            CancellationToken cancellationToken = default)
        {
            var tamano = Math.Max(1, tamanoPagina);
            var consulta = Filtrar(_context.LogsAuditoria.AsNoTracking(), filtro);

            var total = await consulta.CountAsync(cancellationToken);

            // Una página pedida más allá del final (por ejemplo, tras afinar un filtro)
            // se lleva a la última que existe en vez de devolver una página vacía.
            var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamano));
            var pagina = Math.Clamp(numeroPagina, 1, totalPaginas);

            var elementos = await consulta
                .OrderByDescending(l => l.Secuencia)
                .Skip((pagina - 1) * tamano)
                .Take(tamano)
                .ToListAsync(cancellationToken);

            return new Pagina<LogAuditoria>
            {
                Elementos = elementos,
                Total = total,
                NumeroPagina = pagina,
                TamanoPagina = tamano
            };
        }

        public async Task<IReadOnlyList<LogAuditoria>> ListarLoteEnOrdenAsync(
            long? despuesDeSecuencia,
            int tamano,
            CancellationToken cancellationToken = default)
        {
            var consulta = _context.LogsAuditoria.AsNoTracking();

            if (despuesDeSecuencia is { } secuencia)
            {
                consulta = consulta.Where(l => l.Secuencia > secuencia);
            }

            return await consulta
                .OrderBy(l => l.Secuencia)
                .Take(tamano)
                .ToListAsync(cancellationToken);
        }

        public async Task<OpcionesFiltroBitacoraDto> ListarOpcionesDeFiltroAsync(CancellationToken cancellationToken = default)
        {
            var tablas = await _context.LogsAuditoria.AsNoTracking()
                .Select(l => l.NombreTabla)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync(cancellationToken);

            var usuarios = await _context.LogsAuditoria.AsNoTracking()
                .Select(l => l.UsuarioId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return new OpcionesFiltroBitacoraDto { Tablas = tablas, UsuarioIds = usuarios };
        }

        public async Task<IReadOnlyList<MencionEnBitacoraDto>> ListarUltimasMencionesAsync(
            IReadOnlyCollection<string> tablas,
            CancellationToken cancellationToken = default)
        {
            // Arreglo local: Contains sobre un arreglo es lo que EF Core traduce a SQL
            // sin ambigüedad.
            var nombres = tablas.ToArray();

            /* La secuencia más alta de cada registro es su última mención. Se agrupa
               en SQL y solo se traen esas filas, no la bitácora entera. */
            var ultimasSecuencias = _context.LogsAuditoria
                .Where(l => nombres.Contains(l.NombreTabla))
                .GroupBy(l => new { l.NombreTabla, l.RegistroId })
                .Select(g => g.Max(l => l.Secuencia));

            return await _context.LogsAuditoria.AsNoTracking()
                .Where(l => ultimasSecuencias.Contains(l.Secuencia))
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
                .ToListAsync(cancellationToken);
        }

        private static IQueryable<LogAuditoria> Filtrar(IQueryable<LogAuditoria> consulta, FiltroBitacora filtro)
        {
            if (!string.IsNullOrWhiteSpace(filtro.UsuarioId))
            {
                consulta = consulta.Where(l => l.UsuarioId == filtro.UsuarioId);
            }

            if (!string.IsNullOrWhiteSpace(filtro.NombreTabla))
            {
                consulta = consulta.Where(l => l.NombreTabla == filtro.NombreTabla);
            }

            if (!string.IsNullOrWhiteSpace(filtro.TipoAccion))
            {
                consulta = consulta.Where(l => l.TipoAccion == filtro.TipoAccion);
            }

            if (filtro.Desde is { } desde)
            {
                consulta = consulta.Where(l => l.FechaEjecucion >= desde);
            }

            if (filtro.Hasta is { } hasta)
            {
                consulta = consulta.Where(l => l.FechaEjecucion < hasta);
            }

            if (!string.IsNullOrWhiteSpace(filtro.RegistroId))
            {
                var registro = filtro.RegistroId.Trim();
                consulta = consulta.Where(l => l.RegistroId.Contains(registro));
            }

            return consulta;
        }
    }
}
