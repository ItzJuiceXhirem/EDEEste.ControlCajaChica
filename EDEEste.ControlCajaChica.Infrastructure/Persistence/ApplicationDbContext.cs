using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Infrastructure.Identity;

namespace EDEEste.ControlCajaChica.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<Usuario>, IApplicationDbContext
    {
        // Los interceptores se registran en DependencyInjection.AgregarPersistencia
        // (AddInterceptors sobre instancias Singleton) y no se reciben aqui por
        // constructor. Ver el comentario de AuditoriaInterceptor: con instancias
        // Scoped, cualquier forma de conectarlas -- por aqui o por el lambda de
        // AddDbContext -- revienta con ManyServiceProvidersCreatedWarning pasadas ~20
        // peticiones, porque EF ve una instancia de interceptor distinta cada vez.
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Gasto> Gastos { get; set; }
        public DbSet<FondoCajaChica> Fondos { get; set; }
        public DbSet<CategoriaGasto> CategoriasGasto { get; set; }
        public DbSet<SolicitudReposicion> Reposiciones { get; set; }
        public DbSet<ArqueoCaja> Arqueos { get; set; }
        public DbSet<DetalleArqueoDenominacion> DetallesArqueo { get; set; }
        public DbSet<ComprobanteAdjunto> Comprobantes { get; set; }
        public DbSet<LogAuditoria> LogsAuditoria { get; set; }
        public DbSet<Identity.SolicitudPasswordReset> SolicitudesPasswordReset { get; set; }

        /// <summary>
        /// Ver <see cref="IApplicationDbContext.IntentarGuardarCambiosAsync"/>.
        ///
        /// El ChangeTracker.Clear() no es opcional: en Blazor Server el contexto vive
        /// todo el circuito, no la interaccion, asi que si se deja sucio despues de un
        /// fallo, el siguiente clic del usuario -- aunque sea sobre otra pantalla --
        /// arrastra las entidades del intento fallido y vuelve a intentar escribirlas.
        /// </summary>
        public async Task<bool> IntentarGuardarCambiosAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                ChangeTracker.Clear();
                return false;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigurarRelaciones(modelBuilder);
            ConfigurarBitacora(modelBuilder);
            ConfigurarBorradoLogico(modelBuilder);
            ConfigurarLongitudesDeTexto(modelBuilder);
            ConfigurarIndices(modelBuilder);
            ConfigurarConcurrencia(modelBuilder);

            base.OnModelCreating(modelBuilder);

            // Usuario lo descubre base.OnModelCreating (viene de IdentityDbContext),
            // asi que se configura despues.
            modelBuilder.Entity<Identity.Usuario>()
                .Property(u => u.Nombre)
                .HasMaxLength(150);

            // Estas dos recorren el modelo completo, asi que van despues de que EF
            // termino de descubrir los tipos (incluidos los de Identity).
            AplicarPrecisionDeMontos(modelBuilder);
            AplicarLongitudDeFirmas(modelBuilder);
        }

        /// <summary>
        /// Todas las relaciones usan DeleteBehavior.Restrict.
        ///
        /// El borrado fisico no deberia ocurrir nunca: AuditoriaInterceptor convierte
        /// los Remove() en borrado logico. Restrict es la red de seguridad para el
        /// caso en que algo se salte esa ruta: preferimos que la BDD rechace la
        /// operacion antes que arrastrar gastos o comprobantes en cascada y perder
        /// evidencia contable.
        /// </summary>
        private static void ConfigurarRelaciones(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Gasto>(gasto =>
            {
                gasto.HasOne(g => g.FondoCajaChica)
                     .WithMany(f => f.Gastos)
                     .HasForeignKey(g => g.FondoCajaChicaId)
                     .OnDelete(DeleteBehavior.Restrict);

                gasto.HasOne(g => g.CategoriaGasto)
                     .WithMany()
                     .HasForeignKey(g => g.CategoriaGastoId)
                     .OnDelete(DeleteBehavior.Restrict);

                // Opcional: un gasto vive sin reposicion hasta que se incluye en una.
                gasto.HasOne(g => g.Reposicion)
                     .WithMany(r => r.Gastos)
                     .HasForeignKey(g => g.ReposicionId)
                     .IsRequired(false)
                     .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SolicitudReposicion>()
                .HasOne(r => r.FondoCajaChica)
                .WithMany(f => f.Reposiciones)
                .HasForeignKey(r => r.FondoCajaChicaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ArqueoCaja>()
                .HasOne(a => a.FondoCajaChica)
                .WithMany(f => f.Arqueos)
                .HasForeignKey(a => a.FondoCajaChicaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DetalleArqueoDenominacion>(detalle =>
            {
                detalle.HasOne(d => d.ArqueoCaja)
                       .WithMany(a => a.DetallesDenominacion)
                       .HasForeignKey(d => d.ArqueoCajaId)
                       .OnDelete(DeleteBehavior.Restrict);

                // Propiedad calculada: no es una columna.
                detalle.Ignore(d => d.SubtotalDenominacion);
            });

            modelBuilder.Entity<ComprobanteAdjunto>()
                .HasOne(c => c.Gasto)
                .WithMany(g => g.Comprobantes)
                .HasForeignKey(c => c.GastoId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private static void ConfigurarBitacora(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogAuditoria>(log =>
            {
                // Correlativo que fija el orden de la cadena de hashes. Lo genera la
                // BDD para que dos escrituras concurrentes no puedan reclamar el
                // mismo lugar en la cadena.
                log.Property(l => l.Secuencia).ValueGeneratedOnAdd();
                log.HasIndex(l => l.Secuencia).IsUnique();

                log.Property(l => l.HashFirma).HasMaxLength(64);
                log.Property(l => l.HashAnterior).HasMaxLength(64);
            });
        }

        /// <summary>
        /// Indices de consulta.
        ///
        /// El indice de NCF se deja <b>no unico</b> a proposito. Un NCF si es unico en
        /// la practica, pero lo es por proveedor (la secuencia la emite cada proveedor
        /// con su propio RNC), no a nivel global, y ademas conviven NCF de papel con
        /// e-NCF. Poner UNIQUE aqui sin confirmar la regla real con negocio haria que
        /// el sistema rechace facturas legitimas, asi que por ahora solo acelera las
        /// busquedas por NCF, que es para lo que se usa hoy.
        /// </summary>
        /// <summary>
        /// Control de concurrencia optimista sobre tres propiedades que ya existen.
        ///
        /// Re-chequear el estado dentro del handler es necesario pero no basta: dos
        /// usuarios pueden leer la misma solicitud "Aprobada", pasar los dos la
        /// validacion y abonar el fondo dos veces. Marcando estas propiedades, el
        /// UPDATE pasa a llevar "AND columna = @valorOriginal": el segundo en llegar
        /// afecta cero filas, EF lanza DbUpdateConcurrencyException y toda la
        /// transaccion revierte.
        ///
        /// Se usan columnas existentes y NO un rowversion a proposito: la BDD asigna
        /// el rowversion despues de que el interceptor calcula el HMAC, asi que
        /// entrarlo en la firma daria falso positivo de manipulacion en cada relectura,
        /// y dejarlo fuera de la firma obliga a explicar por que una columna del
        /// registro no esta sellada. Con columnas ya firmadas no hay conflicto: el
        /// token compara el valor original, el hash se calcula sobre el actual.
        ///
        /// - FondoCajaChica.BalanceActual sostiene la invariante del dinero.
        /// - SolicitudReposicion.Estado impide aprobar/rechazar/pagar por duplicado.
        /// - Gasto.Estado impide que una anulacion y una reposicion se pisen, y de
        ///   paso que dos reposiciones simultaneas reclamen los mismos gastos.
        /// </summary>
        private static void ConfigurarConcurrencia(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FondoCajaChica>()
                .Property(f => f.BalanceActual)
                .IsConcurrencyToken();

            modelBuilder.Entity<SolicitudReposicion>()
                .Property(r => r.Estado)
                .IsConcurrencyToken();

            modelBuilder.Entity<Gasto>()
                .Property(g => g.Estado)
                .IsConcurrencyToken();
        }

        private static void ConfigurarIndices(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Gasto>(gasto =>
            {
                gasto.HasIndex(g => g.NCF);

                // Es la consulta que corre cada vez que se arma una reposicion.
                gasto.HasIndex(g => new { g.FondoCajaChicaId, g.Estado });
            });

            modelBuilder.Entity<ComprobanteAdjunto>()
                .HasIndex(c => c.GastoId);

            // Es la consulta del sondeo de la pantalla de espera y la que evita
            // duplicar una solicitud Pendiente del mismo usuario.
            modelBuilder.Entity<Identity.SolicitudPasswordReset>()
                .HasIndex(s => new { s.UsuarioId, s.Estado });
        }

        /// <summary>
        /// Filtro global para que nunca traiga registros donde IsDeleted == true al
        /// hacer un query normal. Se aplica a todas las entidades auditables: si una
        /// quedara sin filtro, un registro "borrado" seguiria apareciendo a traves de
        /// sus navegaciones. Para el historial de auditoria hay que usar
        /// IgnoreQueryFilters() explicitamente.
        /// </summary>
        private static void ConfigurarBorradoLogico(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Gasto>().HasQueryFilter(g => !g.IsDeleted);
            modelBuilder.Entity<FondoCajaChica>().HasQueryFilter(f => !f.IsDeleted);
            modelBuilder.Entity<CategoriaGasto>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<SolicitudReposicion>().HasQueryFilter(r => !r.IsDeleted);
            modelBuilder.Entity<ArqueoCaja>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<DetalleArqueoDenominacion>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<ComprobanteAdjunto>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Identity.SolicitudPasswordReset>().HasQueryFilter(s => !s.IsDeleted);
        }

        /// <summary>
        /// Longitudes reales para columnas de texto que hasta ahora quedaban en
        /// nvarchar(max) por convencion. CodigoSolicitud (SolicitudReposicion) y
        /// CodigoArqueo (ArqueoCaja) se dejan fuera a proposito: son codigos internos
        /// propios y aun no se confirmo su formato con negocio.
        /// </summary>
        private static void ConfigurarLongitudesDeTexto(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Gasto>(gasto =>
            {
                // DGII: NCF "de papel" = 11 caracteres, e-NCF electronico = 13. Se usa
                // el mas largo para poder guardar cualquiera de los dos formatos; a
                // nvarchar(N) mas corto no le afecta que quepa un string mas corto.
                gasto.Property(g => g.NCF).HasMaxLength(13);

                // Sin tope legal fijo; se sigue la convencion de 80-150 caracteres que
                // usan los sistemas contables dominicanos, tomando el limite superior.
                gasto.Property(g => g.Proveedor).HasMaxLength(150);

                // RNC (empresa) = 9 digitos, cedula (persona fisica) = 11 digitos, sin
                // guiones. Se usa el mas largo de los dos.
                gasto.Property(g => g.RNCProveedor).HasMaxLength(11);
            });

            modelBuilder.Entity<Gasto>()
                .Property(g => g.Concepto)
                .HasMaxLength(500);

            // Texto libre del mismo tenor que Concepto, asi que se le da la misma cota.
            modelBuilder.Entity<Gasto>()
                .Property(g => g.MotivoAnulacion)
                .HasMaxLength(500);

            modelBuilder.Entity<ComprobanteAdjunto>(comprobante =>
            {
                // Etiqueta corta que digita el custodio por archivo adjunto (no es una
                // regla de negocio externa como NCF/RNC, es propia del sistema).
                comprobante.Property(c => c.Descripcion).HasMaxLength(200);

                // 255 es el tope de nombre de archivo de NTFS y de la mayoria de los
                // sistemas de archivos, asi que un nombre mas largo que esto no pudo
                // haber llegado desde el disco de nadie.
                comprobante.Property(c => c.NombreOriginal).HasMaxLength(255);
                comprobante.Property(c => c.RutaArchivo).HasMaxLength(400);
                comprobante.Property(c => c.TipoMime).HasMaxLength(100);

                // SHA-256 en hexadecimal: siempre 64 caracteres.
                comprobante.Property(c => c.HashSHA256).HasMaxLength(64);
            });

            modelBuilder.Entity<CategoriaGasto>(categoria =>
            {
                categoria.Property(c => c.Nombre).HasMaxLength(100);
                categoria.Property(c => c.CuentaContable).HasMaxLength(50);
            });

            modelBuilder.Entity<SolicitudReposicion>(reposicion =>
            {
                reposicion.Property(r => r.ReferenciaPago).HasMaxLength(100);
                reposicion.Property(r => r.RutaPdfConsolidado).HasMaxLength(400);
            });

            modelBuilder.Entity<ArqueoCaja>()
                .Property(a => a.Observaciones)
                .HasMaxLength(1000);

            modelBuilder.Entity<LogAuditoria>(log =>
            {
                log.Property(l => l.TipoAccion).HasMaxLength(50);

                // 128 es el maximo de un identificador de SQL Server, y esta columna
                // guarda justamente un nombre de tabla.
                log.Property(l => l.NombreTabla).HasMaxLength(128);
                log.Property(l => l.RegistroId).HasMaxLength(100);
                log.Property(l => l.UsuarioId).HasMaxLength(LongitudIdUsuario);
            });

            modelBuilder.Entity<Identity.SolicitudPasswordReset>()
                .Property(s => s.TokenReseteo)
                // Los tokens de DataProtection que emite Identity son base64 y no
                // tienen un tope documentado, pero en la practica no pasan de unos
                // pocos cientos de caracteres; 1000 deja margen sin dejarlo en
                // nvarchar(max).
                .HasMaxLength(1000);

            modelBuilder.Entity<Identity.SolicitudPasswordReset>()
                .Property(s => s.HashSecreto)
                // SHA-256 en hexadecimal son siempre 64 caracteres.
                .HasMaxLength(64);

            AplicarLongitudDeIdsDeUsuario(modelBuilder);
        }

        /// <summary>
        /// Longitud de las columnas que guardan el Id de un usuario de Identity.
        /// AspNetUsers.Id es nvarchar(450) (el maximo indexable de SQL Server), asi que
        /// cualquier columna que lo referencie tiene que aguantar lo mismo.
        /// </summary>
        private const int LongitudIdUsuario = 450;

        private static void AplicarLongitudDeIdsDeUsuario(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FondoCajaChica>()
                .Property(f => f.CustodioId).HasMaxLength(LongitudIdUsuario);

            modelBuilder.Entity<Gasto>()
                .Property(g => g.RegistradoPorUsuarioId).HasMaxLength(LongitudIdUsuario);

            modelBuilder.Entity<ArqueoCaja>()
                .Property(a => a.RealizadoPorUsuarioId).HasMaxLength(LongitudIdUsuario);

            modelBuilder.Entity<SolicitudReposicion>(reposicion =>
            {
                reposicion.Property(r => r.SolicitoUsuarioId).HasMaxLength(LongitudIdUsuario);
                reposicion.Property(r => r.GerenteUsuarioId).HasMaxLength(LongitudIdUsuario);
                reposicion.Property(r => r.FinanzasUsuarioId).HasMaxLength(LongitudIdUsuario);
            });

            modelBuilder.Entity<Identity.SolicitudPasswordReset>(solicitud =>
            {
                solicitud.Property(s => s.UsuarioId).HasMaxLength(LongitudIdUsuario);
                solicitud.Property(s => s.ResueltaPorUsuarioId).HasMaxLength(LongitudIdUsuario);
            });

            // CreadoPorId / ModificadoPorId estan en AuditableEntity, asi que en vez de
            // repetirlos entidad por entidad se recorre el modelo: cualquier entidad
            // auditable que se agregue despues los hereda ya configurados.
            var tiposAuditables = modelBuilder.Model
                .GetEntityTypes()
                .Where(tipo => typeof(AuditableEntity).IsAssignableFrom(tipo.ClrType));

            foreach (var tipo in tiposAuditables)
            {
                tipo.FindProperty(nameof(AuditableEntity.CreadoPorId))?.SetMaxLength(LongitudIdUsuario);
                tipo.FindProperty(nameof(AuditableEntity.ModificadoPorId))?.SetMaxLength(LongitudIdUsuario);
            }
        }

        /// <summary>
        /// Fija decimal(18,4) para todos los montos.
        ///
        /// No es cosmetico: sin esto EF usa decimal(18,2) por convencion y SQL Server
        /// trunca en silencio. Como la firma HMAC se calcula en memoria sobre el valor
        /// original (normalizado a 4 decimales por ConstructorFirma), al releer la fila
        /// el valor truncado ya no produciria el mismo hash y el sistema reportaria una
        /// manipulacion que nunca ocurrio. La escala de la columna y la del hash tienen
        /// que ser la misma.
        /// </summary>
        private static void AplicarPrecisionDeMontos(ModelBuilder modelBuilder)
        {
            var propiedadesDecimales = modelBuilder.Model
                .GetEntityTypes()
                .SelectMany(tipo => tipo.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));

            foreach (var propiedad in propiedadesDecimales)
            {
                propiedad.SetPrecision(18);
                propiedad.SetScale(4);
            }
        }

        /// <summary>
        /// Un HMAC-SHA256 en hexadecimal siempre mide 64 caracteres, asi que no hay
        /// razon para dejar estas columnas como nvarchar(max).
        /// </summary>
        private static void AplicarLongitudDeFirmas(ModelBuilder modelBuilder)
        {
            var tiposFirmados = modelBuilder.Model
                .GetEntityTypes()
                .Where(tipo => typeof(ITamperProofEntity).IsAssignableFrom(tipo.ClrType));

            foreach (var tipo in tiposFirmados)
            {
                tipo.FindProperty(nameof(ITamperProofEntity.HashFirma))?.SetMaxLength(64);
            }
        }
    }
}
