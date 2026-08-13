using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using EDEEste.ControlCajaChica.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence.Interceptors
{
    public class AuditoriaInterceptor : SaveChangesInterceptor
    {
        private readonly ICriptografiaService _criptografiaService;
        private readonly ICurrentUserService _currentUserService;

        public AuditoriaInterceptor(ICriptografiaService criptografiaService, ICurrentUserService currentUserService)
        {
            _criptografiaService = criptografiaService;
            _currentUserService = currentUserService;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            var context = eventData.Context;
            if (context == null) return base.SavingChanges(eventData, result);

            var usuarioActual = _currentUserService.UserId ?? "Sistema";
            var auditoriaEntries = new List<LogAuditoria>();

            // ChangeTracker da acceso a todas las entidades que EF Core está por guardar
            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
                    continue;

                // llenado automático de fechas y usuarios para auditoría base
                if (entry.Entity is AuditableEntity auditable)
                {
                    if (entry.State == EntityState.Added)
                    {
                        auditable.CreadoPorId = usuarioActual;
                        auditable.FechaCreacion = DateTime.UtcNow;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        auditable.ModificadoPorId = usuarioActual;
                        auditable.FechaModificacion = DateTime.UtcNow;
                    }
                    else if (entry.State == EntityState.Deleted)
                    {
                        // se intercepta el borrado físico de la BDD y lo convertimos en lógico
                        entry.State = EntityState.Modified;
                        auditable.IsDeleted = true;
                        auditable.ModificadoPorId = usuarioActual;
                        auditable.FechaModificacion = DateTime.UtcNow;
                    }
                }

                // trazabilidad JSON para la tabla de Logs
                if (entry.Entity is not LogAuditoria)
                {
                    var log = new LogAuditoria
                    {
                        UsuarioId = usuarioActual,
                        TipoAccion = entry.State.ToString(),
                        NombreTabla = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                        RegistroId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "N/A"
                    };

                    // OriginalValues tiene el estado de la fila antes del cambio
                    if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                    {
                        var originalValues = entry.OriginalValues.Properties.ToDictionary(p => p.Name, p => entry.OriginalValues[p]);
                        log.ValoresAnteriores = JsonSerializer.Serialize(originalValues);
                    }

                    // CurrentValues tiene el nuevo estado que se va a guardar
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    {
                        var currentValues = entry.CurrentValues.Properties.ToDictionary(p => p.Name, p => entry.CurrentValues[p]);
                        log.ValoresNuevos = JsonSerializer.Serialize(currentValues);
                    }

                    auditoriaEntries.Add(log);
                }

                // sello criptográfico (para que no puedan hacer manipulación directa desde la BDD)
                if (entry.Entity is ITamperProofEntity tamperEntity &&
                   (entry.State == EntityState.Added || entry.State == EntityState.Modified))
                {
                    tamperEntity.HashFirma = _criptografiaService.CalcularHMAC(tamperEntity.ObtenerCadenaParaHash());
                }
            }

            // se agregan los logs al mismo contexto para que se guarden en la misma transacción SQL
            if (auditoriaEntries.Any())
            {
                context.AddRange(auditoriaEntries);
            }

            return base.SavingChanges(eventData, result);
        }
    }
}
