using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Infrastructure.Persistence.Interceptors;

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        private readonly AuditoriaInterceptor _auditoriaInterceptor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, AuditoriaInterceptor auditoriaInterceptor)
            : base(options)
        {
            _auditoriaInterceptor = auditoriaInterceptor;
        }

        public DbSet<Gasto> Gastos { get; set; }
        public DbSet<FondoCajaChica> Fondos { get; set; }
        public DbSet<LogAuditoria> LogsAuditoria { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_auditoriaInterceptor);
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // filtro global para que nunca traiga registros donde IsDeleted == true al hacer un query normal
            modelBuilder.Entity<Gasto>().HasQueryFilter(g => !g.IsDeleted);
            modelBuilder.Entity<FondoCajaChica>().HasQueryFilter(f => !f.IsDeleted);

            base.OnModelCreating(modelBuilder);
        }
    }
}
