using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// Lee las tablas de negocio para la revisión de integridad. Todas las consultas
    /// ignoran el filtro de borrado lógico: un registro eliminado también tiene que
    /// estar íntegro, y también cuenta como existente.
    ///
    /// No recalcula ninguna firma a mano: IntegridadInterceptor ya marca
    /// IntegridadVerificada en cada entidad que se materializa, también en consultas
    /// AsNoTracking. Aquí solo se recoge lo que ese interceptor dejó marcado.
    /// </summary>
    public sealed class VerificadorIntegridadDatos : IVerificadorIntegridadDatos
    {
        private readonly ApplicationDbContext _context;

        public VerificadorIntegridadDatos(ApplicationDbContext context) => _context = context;

        public async Task<IReadOnlyList<RegistroConFirmaInvalidaDto>> ListarFirmasInvalidasAsync(
            CancellationToken cancellationToken = default)
        {
            // Las entidades que implementan ITamperProofEntity. Si se agrega otra, va aquí.
            var resultado = new List<RegistroConFirmaInvalidaDto>();

            resultado.AddRange(await ListarComprometidosAsync(_context.Fondos, f => f.Id,
                f => $"Fondo fijo RD$ {f.MontoFijo:N2} · balance RD$ {f.BalanceActual:N2}", cancellationToken));

            resultado.AddRange(await ListarComprometidosAsync(_context.Gastos, g => g.Id,
                g => $"{g.Proveedor} · NCF {g.NCF} · RD$ {g.MontoTotal:N2}", cancellationToken));

            resultado.AddRange(await ListarComprometidosAsync(_context.Comprobantes, c => c.Id,
                c => $"{c.NombreOriginal} · {c.Descripcion}", cancellationToken));

            resultado.AddRange(await ListarComprometidosAsync(_context.Reposiciones, r => r.Id,
                r => $"RD$ {r.MontoReclamado:N2} · {r.Estado}", cancellationToken));

            resultado.AddRange(await ListarComprometidosAsync(_context.Arqueos, a => a.Id,
                a => $"{a.FechaArqueo:dd/MM/yyyy} · {a.Resultado} · diferencia RD$ {a.Diferencia:N2}", cancellationToken));

            return resultado;
        }

        public async Task<IReadOnlyDictionary<string, IReadOnlySet<Guid>>> ObtenerIdsExistentesAsync(
            CancellationToken cancellationToken = default)
        {
            /* Las mismas tablas que llevan filtro de borrado lógico en
               ApplicationDbContext.ConfigurarBorradoLogico: en ellas nada se borra de
               verdad nunca, así que un registro que falta lo borró alguien por fuera de
               la aplicación. Si se agrega una tabla con borrado lógico, va en los dos
               sitios. */
            var resultado = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);

            await AgregarIdsAsync(resultado, _context.Gastos, g => g.Id, cancellationToken);
            await AgregarIdsAsync(resultado, _context.Fondos, f => f.Id, cancellationToken);
            await AgregarIdsAsync(resultado, _context.CategoriasGasto, c => c.Id, cancellationToken);
            await AgregarIdsAsync(resultado, _context.Reposiciones, r => r.Id, cancellationToken);
            await AgregarIdsAsync(resultado, _context.Arqueos, a => a.Id, cancellationToken);
            await AgregarIdsAsync(resultado, _context.DetallesArqueo, d => d.Id, cancellationToken);
            await AgregarIdsAsync(resultado, _context.Comprobantes, c => c.Id, cancellationToken);
            await AgregarIdsAsync(resultado, _context.SolicitudesPasswordReset, s => s.Id, cancellationToken);

            return resultado;
        }

        private async Task<IEnumerable<RegistroConFirmaInvalidaDto>> ListarComprometidosAsync<T>(
            DbSet<T> conjunto,
            Func<T, Guid> clave,
            Func<T, string> describir,
            CancellationToken cancellationToken)
            where T : AuditableEntity, ITamperProofEntity
        {
            var filas = await conjunto.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
            var tabla = NombreTabla<T>();

            return filas
                .Where(fila => !fila.IntegridadVerificada)
                .Select(fila => new RegistroConFirmaInvalidaDto
                {
                    NombreTabla = tabla,
                    Id = clave(fila),
                    Descripcion = describir(fila),
                    FechaCreacion = fila.FechaCreacion,
                    FechaModificacion = fila.FechaModificacion,
                    Eliminado = fila.IsDeleted
                });
        }

        private async Task AgregarIdsAsync<T>(
            Dictionary<string, IReadOnlySet<Guid>> destino,
            DbSet<T> conjunto,
            Expression<Func<T, Guid>> clave,
            CancellationToken cancellationToken)
            where T : class
        {
            // Solo la columna de la clave: no hace falta materializar las entidades.
            var ids = await conjunto.IgnoreQueryFilters().AsNoTracking().Select(clave).ToListAsync(cancellationToken);
            destino[NombreTabla<T>()] = ids.ToHashSet();
        }

        /* El mismo nombre que AuditoriaInterceptor guarda en LogAuditoria.NombreTabla
           (entry.Metadata.GetTableName()), tomado del modelo y no escrito a mano: así
           los dos lados no pueden desalinearse. */
        private string NombreTabla<T>() =>
            _context.Model.FindEntityType(typeof(T))?.GetTableName()
            ?? throw new InvalidOperationException($"La entidad {typeof(T).Name} no está mapeada a ninguna tabla.");
    }
}
