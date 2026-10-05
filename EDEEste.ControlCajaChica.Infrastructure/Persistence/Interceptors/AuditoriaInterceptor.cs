
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
    /// Corre en cada SaveChanges: rellena los campos de auditoría, convierte los
    /// borrados físicos en lógicos, sella con HMAC las entidades firmadas y escribe
    /// la bitácora encadenada, todo dentro de la misma transacción del guardado.
    ///
    /// Se registra Singleton (ver DependencyInjection.AgregarPersistencia) para que
    /// EF vea siempre la MISMA instancia y no reconstruya su proveedor de servicios
    /// interno en cada petición (se probaron dos formas de pasar un interceptor Scoped
    /// -- por el lambda de AddDbContext, y por constructor de ApplicationDbContext +
    /// OnConfiguring -- y las dos revientan con ManyServiceProvidersCreatedWarning
    /// pasadas ~20 peticiones, porque en ambas EF ve una instancia distinta cada vez).
    ///
    /// Por ser Singleton, NO puede recibir ICurrentUserService (Scoped) por
    /// constructor. Se lo pide al ApplicationDbContext que está guardando, que sí es
    /// Scoped (ver ApplicationDbContext.ObtenerUsuarioAuditoriaAsync). No usar un
    /// AsyncLocal asignado dentro de un método async para esto: el método restaura el
    /// ExecutionContext al salir, el valor nunca llega al llamador, y toda la
    /// bitácora quedaba a nombre de "Sistema".
    /// </summary>
    public class AuditoriaInterceptor : SaveChangesInterceptor
    {
        private const string UsuarioSistema = "Sistema";
        private const string MarcadorRedactado = "***";

        // Nombre del lock nombrado de SQL Server (sp_getapplock) que serializa la
        // lectura del último HashFirma con el INSERT de los logs nuevos. Ver el
        // comentario de FirmarCadenaDeLogs para el porqué, y el de
        // ApplicationDbContext.SaveChangesAsync para el porqué del @LockOwner='Transaction'.
        private const string RecursoLockCadena = "LogsAuditoria:Cadena";

        // 5 segundos de margen generoso: un guardado normal (unas pocas filas de
        // bitácora) toma milisegundos, así que esta espera solo se nota si algo más
        // está genuinamente atascado reteniendo el lock.
        private const string ScriptTomarLockDeCadena = """
            DECLARE @resultado int;
            EXEC @resultado = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000;
            IF @resultado < 0
            BEGIN
                THROW 50000, 'No se pudo obtener el lock de la cadena de auditoría a tiempo. Nada se guardó todavía; probablemente otra persona registraba un cambio al mismo tiempo. Vuelva a intentarlo en unos segundos.', 1;
            END
            """;

        /// <summary>
        /// Propiedades que JAMÁS deben llegar a LogsAuditoria en claro, aunque
        /// pertenezcan a una entidad legítima que sí se audita (AspNetUsers,
        /// SolicitudesPasswordReset). LogsAuditoria es una bitácora de negocio para
        /// que un Auditor vea "quién cambio qué" -- no un lugar donde deba poder leerse
        /// un hash de contraseña o un secreto de un solo uso, ni siquiera un DBA con
        /// acceso de lectura a la BDD.
        /// </summary>
        private static readonly HashSet<string> PropiedadesSensibles = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(Identity.Usuario.PasswordHash),
            nameof(Identity.Usuario.SecurityStamp),
            nameof(Identity.Usuario.ConcurrencyStamp),
            nameof(Identity.SolicitudPasswordReset.TokenReseteo),
            nameof(Identity.SolicitudPasswordReset.HashSecreto),
        };

        private readonly ICriptografiaService _criptografiaService;
        private readonly ILogger<AuditoriaInterceptor> _logger;

        public AuditoriaInterceptor(
            ICriptografiaService criptografiaService,
            ILogger<AuditoriaInterceptor> logger)
        {
            _criptografiaService = criptografiaService;
            _logger = logger;
        }

      /* EF llama SavingChanges para SaveChanges() y SavingChangesAsync para
         SaveChangesAsync(). Hay que sobreescribir las dos: antes solo estaba la
         version síncrona, así que toda la auditoría se saltaba en el camino async,
         que es justamente el que usa la aplicación (IApplicationDbContext solo
         expone SaveChangesAsync). */
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context is not null)
            {
              /* El lado síncrono no puede esperar a ICurrentUserService.ObtenerAsync.
                 Ningún camino de la app guarda de forma síncrona (ver
                 ApplicationDbContext.SaveChanges), así que aquí queda "Sistema". */
                var logs = PrepararEntidades(eventData.Context, UsuarioSistema);

                if (logs.Count > 0)
                {
                    TomarLockDeCadena(eventData.Context);

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
                var usuarioId = await ResolverUsuarioAsync(eventData.Context, cancellationToken);
                var logs = PrepararEntidades(eventData.Context, usuarioId);

                if (logs.Count > 0)
                {
                    await TomarLockDeCadenaAsync(eventData.Context, cancellationToken);

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

        private static async Task<string> ResolverUsuarioAsync(DbContext context, CancellationToken cancellationToken) =>
            context is ApplicationDbContext aplicacion
                ? await aplicacion.ObtenerUsuarioAuditoriaAsync(cancellationToken) ?? UsuarioSistema
                : UsuarioSistema;

        /// <summary>
        /// Recorre el ChangeTracker aplicando auditoría y firma, y devuelve los logs
        /// listos para encadenar. No hace IO para poder compartirse entre la ruta
        /// síncrona y la asíncrona.
        /// </summary>
        private List<LogAuditoria> PrepararEntidades(DbContext context, string usuarioId)
        {
            var logs = new List<LogAuditoria>();

          /* Se materializa la lista antes de recorrerla porque abajo se modifica el
             State de algunas entradas (borrado físico -> lógico). */
            var entradas = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => e.Entity is not LogAuditoria)
                .ToList();

            foreach (var entry in entradas)
            {
              /* Se guarda el estado real antes de tocarlo: si no, un borrado lógico
                 quedaría registrado en la bitácora como un simple "Modified". */
                var estadoOriginal = entry.State;

                AplicarCamposDeAuditoria(entry, usuarioId);

              /* La firma se calcula ANTES de fotografiar CurrentValues, para que el
                 log guarde exactamente el mismo HashFirma que termina en la BDD. */
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
        /// Sello criptográfico para que no puedan manipular la fila directamente desde
        /// la BDD. Antes de re-firmar se comprueba que lo que se leyó no venía ya
        /// alterado: de lo contrario la aplicación "lavaría" el fraude firmando de
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
                    "Se bloqueó un guardado sobre {Entidad} '{RegistroId}': la firma almacenada no coincide con los datos.",
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
                    .ToDictionary(p => p.Name, p => RedactarSiEsSensible(p.Name, entry.OriginalValues[p]));
                log.ValoresAnteriores = JsonSerializer.Serialize(valoresAnteriores);
            }

            /* CurrentValues tiene el nuevo estado que se va a guardar. También se
               registra para el borrado lógico, porque ahí sí hay una fila resultante. */
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var valoresNuevos = entry.CurrentValues.Properties
                    .ToDictionary(p => p.Name, p => RedactarSiEsSensible(p.Name, entry.CurrentValues[p]));
                log.ValoresNuevos = JsonSerializer.Serialize(valoresNuevos);
            }

            return log;
        }

        /// <summary>
        /// Se redacta con un marcador en vez de omitir la clave: la bitácora sigue
        /// dejando constancia de que ese campo cambió (para "SecurityStamp cambio" es
        /// dato útil -- indica un cierre de sesión forzado), sin revelar el valor.
        /// </summary>
        private static object? RedactarSiEsSensible(string nombrePropiedad, object? valor) =>
            PropiedadesSensibles.Contains(nombrePropiedad) && valor is not null
                ? MarcadorRedactado
                : valor;

        /// <summary>
        /// Toma el lock nombrado dentro de la transacción que envuelve este guardado
        /// (ver ApplicationDbContext.SaveChanges/SaveChangesAsync). Sin él, dos
        /// SaveChanges concurrentes podían leer el mismo HashFirma "último" -- el que
        /// se lee justo después de esta llamada -- y encadenar los dos logs desde
        /// ahí, bifurcando la cadena.
        /// </summary>
        private static void TomarLockDeCadena(DbContext context) =>
            context.Database.ExecuteSqlRaw(ScriptTomarLockDeCadena, RecursoLockCadena);

        private static Task TomarLockDeCadenaAsync(DbContext context, CancellationToken cancellationToken) =>
            context.Database.ExecuteSqlRawAsync(ScriptTomarLockDeCadena, [RecursoLockCadena], cancellationToken);

        /* Enlaza cada log con la firma del anterior. Romper un eslabón (borrar o
           editar una fila) deja la cadena inconsistente y por lo tanto detectable. */
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
