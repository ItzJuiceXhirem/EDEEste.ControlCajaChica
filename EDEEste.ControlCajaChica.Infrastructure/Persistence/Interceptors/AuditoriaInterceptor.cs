using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Exceptions;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Interceptors
{
    /// <summary>
    /// Corre en cada SaveChanges: rellena los campos de auditoria, convierte los
    /// borrados fisicos en logicos, sella con HMAC las entidades firmadas y escribe
    /// la bitacora encadenada, todo dentro de la misma transaccion del guardado.
    ///
    /// Se registra Singleton (ver DependencyInjection.AgregarPersistencia) para que
    /// EF vea siempre la MISMA instancia y no reconstruya su proveedor de servicios
    /// interno en cada peticion (se probaron dos formas de pasar un interceptor Scoped
    /// -- por el lambda de AddDbContext, y por constructor de ApplicationDbContext +
    /// OnConfiguring -- y las dos revientan con ManyServiceProvidersCreatedWarning
    /// pasadas ~20 peticiones, porque en ambas EF ve una instancia distinta cada vez).
    ///
    /// Por ser Singleton, NO puede recibir ICurrentUserService (Scoped) por
    /// constructor. En su lugar lee AmbientUsuarioActual, un AsyncLocal que
    /// ICurrentUserService.ObtenerAsync() deja puesto -- ver ese archivo y el
    /// comentario de AmbientUsuarioActual para el porque esto es seguro.
    /// </summary>
    public class AuditoriaInterceptor : SaveChangesInterceptor
    {
        private const string UsuarioSistema = "Sistema";

        private readonly ICriptografiaService _criptografiaService;
        private readonly ILogger<AuditoriaInterceptor> _logger;

        public AuditoriaInterceptor(
            ICriptografiaService criptografiaService,
            ILogger<AuditoriaInterceptor> logger)
        {
            _criptografiaService = criptografiaService;
            _logger = logger;
        }

        // EF llama SavingChanges para SaveChanges() y SavingChangesAsync para
        // SaveChangesAsync(). Hay que sobreescribir las dos: antes solo estaba la
        // version sincrona, asi que toda la auditoria se saltaba en el camino async,
        // que es justamente el que usa la aplicacion (IApplicationDbContext solo
        // expone SaveChangesAsync).
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context is not null)
            {
                var usuarioId = ResolverUsuario();
                var logs = PrepararEntidades(eventData.Context, usuarioId);

                if (logs.Count > 0)
                {
                    var hashPrevio = eventData.Context.Set<LogAuditoria>()
                        .AsNoTracking()
                        .OrderByDescending(l => l.Secuencia)
                        .Select(l => l.HashFirma)
                        .FirstOrDefault();

                    FirmarCadenaDeLogs(logs, hashPrevio);
                    eventData.Context.AddRange(logs);
                }
            }

            return base.SavingChanges(eventData, result);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null)
            {
                var usuarioId = ResolverUsuario();
                var logs = PrepararEntidades(eventData.Context, usuarioId);

                if (logs.Count > 0)
                {
                    var hashPrevio = await eventData.Context.Set<LogAuditoria>()
                        .AsNoTracking()
                        .OrderByDescending(l => l.Secuencia)
                        .Select(l => l.HashFirma)
                        .FirstOrDefaultAsync(cancellationToken);

                    FirmarCadenaDeLogs(logs, hashPrevio);
                    eventData.Context.AddRange(logs);
                }
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static string ResolverUsuario() => AmbientUsuarioActual.UsuarioId ?? UsuarioSistema;

        /// <summary>
        /// Recorre el ChangeTracker aplicando auditoria y firma, y devuelve los logs
        /// listos para encadenar. No hace IO para poder compartirse entre la ruta
        /// sincrona y la asincrona.
        /// </summary>
        private List<LogAuditoria> PrepararEntidades(DbContext context, string usuarioId)
        {
            var logs = new List<LogAuditoria>();

            // Se materializa la lista antes de recorrerla porque abajo se modifica el
            // State de algunas entradas (borrado fisico -> logico).
            var entradas = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => e.Entity is not LogAuditoria)
                .ToList();

            foreach (var entry in entradas)
            {
                // Se guarda el estado real antes de tocarlo: si no, un borrado logico
                // quedaria registrado en la bitacora como un simple "Modified".
                var estadoOriginal = entry.State;

                AplicarCamposDeAuditoria(entry, usuarioId);

                // La firma se calcula ANTES de fotografiar CurrentValues, para que el
                // log guarde exactamente el mismo HashFirma que termina en la BDD.
                AplicarSelloDeIntegridad(entry);

                logs.Add(ConstruirLog(entry, estadoOriginal, usuarioId));
            }

            return logs;
        }

        private static void AplicarCamposDeAuditoria(EntityEntry entry, string usuarioId)
        {
            if (entry.Entity is not AuditableEntity auditable)
            {
                return;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreadoPorId = usuarioId;
                    auditable.FechaCreacion = DateTime.UtcNow;
                    break;

                case EntityState.Modified:
                    auditable.ModificadoPorId = usuarioId;
                    auditable.FechaModificacion = DateTime.UtcNow;
                    break;

                case EntityState.Deleted:
                    // se intercepta el borrado físico de la BDD y lo convertimos en lógico
                    entry.State = EntityState.Modified;
                    auditable.IsDeleted = true;
                    auditable.ModificadoPorId = usuarioId;
                    auditable.FechaModificacion = DateTime.UtcNow;
                    break;
            }
        }

        /// <summary>
        /// Sello criptografico para que no puedan manipular la fila directamente desde
        /// la BDD. Antes de re-firmar se comprueba que lo que se leyo no venia ya
        /// alterado: de lo contrario la aplicacion "lavaria" el fraude firmando de
        /// nuevo sobre los datos adulterados.
        /// </summary>
        private void AplicarSelloDeIntegridad(EntityEntry entry)
        {
            if (entry.Entity is not ITamperProofEntity entidadFirmada)
            {
                return;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                return;
            }

            if (!entidadFirmada.IntegridadVerificada)
            {
                var registroId = ObtenerClavePrimaria(entry);
                _logger.LogCritical(
                    "Se bloqueo un guardado sobre {Entidad} '{RegistroId}': la firma almacenada no coincide con los datos.",
                    entry.Entity.GetType().Name,
                    registroId);

                throw new IntegridadComprometidaException(entry.Entity.GetType().Name, registroId);
            }

            entidadFirmada.HashFirma = _criptografiaService.CalcularHMAC(entidadFirmada.ObtenerCadenaParaHash());
        }

        private static LogAuditoria ConstruirLog(EntityEntry entry, EntityState estadoOriginal, string usuarioId)
        {
            var log = new LogAuditoria
            {
                UsuarioId = usuarioId,
                TipoAccion = estadoOriginal.ToString(),
                NombreTabla = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                RegistroId = ObtenerClavePrimaria(entry)
            };

            // OriginalValues tiene el estado de la fila antes del cambio
            if (estadoOriginal is EntityState.Modified or EntityState.Deleted)
            {
                var valoresAnteriores = entry.OriginalValues.Properties
                    .ToDictionary(p => p.Name, p => entry.OriginalValues[p]);
                log.ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores);
            }

            // CurrentValues tiene el nuevo estado que se va a guardar. Tambien se
            // registra para el borrado logico, porque ahi si hay una fila resultante.
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var valoresNuevos = entry.CurrentValues.Properties
                    .ToDictionary(p => p.Name, p => entry.CurrentValues[p]);
                log.ValoresNuevos = JsonSerializer.Serialize(valoresNuevos);
            }

            return log;
        }

        /// <summary>
        /// Enlaza cada log con la firma del anterior. Romper un eslabon (borrar o
        /// editar una fila) deja la cadena inconsistente y por lo tanto detectable.
        /// </summary>
        private void FirmarCadenaDeLogs(List<LogAuditoria> logs, string? hashPrevio)
        {
            var anterior = string.IsNullOrEmpty(hashPrevio) ? LogAuditoria.HashGenesis : hashPrevio;

            foreach (var log in logs)
            {
                log.HashAnterior = anterior;
                log.HashFirma = _criptografiaService.CalcularHMAC(log.ObtenerCadenaParaHash());
                anterior = log.HashFirma;
            }
        }

        private static string ObtenerClavePrimaria(EntityEntry entry) =>
            entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "N/A";
    }
}
