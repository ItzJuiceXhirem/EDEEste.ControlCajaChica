using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Reposiciones
    {
        private const decimal PorcentajeAlertaPorDefecto = 30m;

        [Inject]
        private IFondoRepository Fondos { get; set; } = default!;

        [Inject]
        private IGastoRepository RepositorioGastos { get; set; } = default!;

        [Inject]
        private IReposicionRepository RepositorioReposiciones { get; set; } = default!;

        [Inject]
        private CrearSolicitudReposicionHandler HandlerGenerar { get; set; } = default!;

        [Inject]
        private AprobarReposicionHandler HandlerAprobar { get; set; } = default!;

        [Inject]
        private ProcesarPagoReposicionHandler HandlerPagar { get; set; } = default!;

        [Inject]
        private IIdentityService Identidad { get; set; } = default!;

        [Inject]
        private ICurrentUserService UsuarioActual { get; set; } = default!;

        [Inject]
        private AuthenticationStateProvider EstadoDeAutenticacion { get; set; } = default!;

        [Inject]
        private IAuthorizationService Autorizacion { get; set; } = default!;

        // CustodioId, GerenteUsuarioId y FinanzasUsuarioId guardan el Id de Identity
        // (un GUID), no un nombre: sin este mapa, la pantalla mostraria el GUID crudo
        // en vez del nombre de usuario donde sea que aparezcan.
        private readonly Dictionary<string, string> nombresDeUsuario = new();

        private IReadOnlyList<FondoCajaChica>? fondos;
        private FondoCajaChica? fondoActual;
        private IReadOnlyList<Gasto>? pendientes;
        private IReadOnlyList<SolicitudReposicion>? solicitudes;
        private IReadOnlyList<SolicitudReposicion>? porAprobar;
        private IReadOnlyList<SolicitudReposicion>? porPagar;
        private Guid fondoSeleccionado;

        // Resueltos una sola vez al cargar: deciden cual de las cuatro plantillas de
        // bandeja (direccion A - Bandeja, la version confirmada) se muestra. Los
        // botones de accion, ademas, quedan siempre gateados por su propio
        // AuthorizeView -- esto solo elige el diseño de pantalla, no reemplaza esa
        // comprobacion ni la de los handlers.
        private bool puedeSolicitar;
        private bool puedeAprobar;
        private bool puedePagar;
        private bool EsSoloConsulta => !puedeSolicitar && !puedeAprobar && !puedePagar;

        // Bloquea todos los botones de accion mientras se procesa una solicitud, no
        // solo el de la fila que se pulso: dos clics sobre solicitudes distintas
        // podrian competir por el mismo fondo.
        private Guid? procesando;
        private readonly Dictionary<Guid, string> referencias = new();

        // null = todo el historial. Filtra en memoria sobre "solicitudes", que ya
        // trae todo lo del fondo elegido de una sola vez.
        private int? diasSolicitudes = 90;
        private string busquedaSolicitudes = string.Empty;

        private readonly List<string> errores = new();
        private string? exito;
        private bool generando;

        protected override async Task OnInitializedAsync()
        {
            var estado = await EstadoDeAutenticacion.GetAuthenticationStateAsync();
            var usuario = estado.User;
            puedeSolicitar = (await Autorizacion.AuthorizeAsync(usuario, Permisos.SolicitarReposicion)).Succeeded;
            puedeAprobar = (await Autorizacion.AuthorizeAsync(usuario, Permisos.AprobarReposicion)).Succeeded;
            puedePagar = (await Autorizacion.AuthorizeAsync(usuario, Permisos.PagarReposicion)).Succeeded;

            fondos = await FondosVisiblesAsync();
            await ResolverNombresDeUsuarioAsync(fondos.Select(f => f.CustodioId));

            if (fondos.Count > 0)
            {
                fondoSeleccionado = fondos[0].Id;
                await CargarFondoAsync(fondoSeleccionado);
            }

            await CargarColasAsync();
        }

        /// <summary>
        /// Un Custodio solo debe ver el fondo que tiene a cargo: sin este filtro, el
        /// desplegable le dejaba elegir el fondo de cualquier otro custodio y ver
        /// sus gastos pendientes y su historial de solicitudes. Gerente/Finanzas/
        /// Auditor no tienen restriccion: trabajan por cola o por consulta general.
        /// </summary>
        private async Task<IReadOnlyList<FondoCajaChica>> FondosVisiblesAsync()
        {
            var todos = await Fondos.ListarAsync();
            var usuario = await UsuarioActual.ObtenerAsync();

            if (usuario.Id is not { } usuarioId || !await Identidad.EstaEnRolAsync(usuarioId, RolesApp.Custodio))
            {
                return todos;
            }

            return todos.Where(f => f.CustodioId == usuarioId).ToList();
        }

        /// <summary>
        /// Traduce Ids de Identity (custodio, gerente, finanzas) a nombres legibles.
        /// Cachea en nombresDeUsuario para no repetir la consulta cuando el mismo
        /// usuario ya se resolvio antes (por ejemplo, el mismo gerente aprobando
        /// varias solicitudes).
        /// </summary>
        private async Task ResolverNombresDeUsuarioAsync(IEnumerable<string?> ids)
        {
            var pendientesDeResolver = ids
                .Where(id => !string.IsNullOrEmpty(id) && !nombresDeUsuario.ContainsKey(id!))
                .Select(id => id!)
                .Distinct()
                .ToList();

            if (pendientesDeResolver.Count == 0)
            {
                return;
            }

            var resueltos = await Identidad.ObtenerNombresUsuarioAsync(pendientesDeResolver);
            foreach (var id in pendientesDeResolver)
            {
                nombresDeUsuario[id] = resueltos.GetValueOrDefault(id, id);
            }
        }

        private string NombreCustodio(string custodioId) =>
            string.IsNullOrWhiteSpace(custodioId) ? "(sin asignar)" : nombresDeUsuario.GetValueOrDefault(custodioId, custodioId);

        private string NombreUsuario(string? usuarioId) =>
            string.IsNullOrEmpty(usuarioId) ? "-" : nombresDeUsuario.GetValueOrDefault(usuarioId, usuarioId);

        /// <summary>
        /// Las colas de aprobacion y pago no dependen del fondo seleccionado: el
        /// Gerente y Finanzas trabajan por cola, no por fondo, y ni siquiera todos
        /// tienen el permiso para elegir un fondo.
        /// </summary>
        private async Task CargarColasAsync()
        {
            porAprobar = await RepositorioReposiciones.ListarPorEstadoAsync(EstadoReposicion.PendienteAprobacion);
            porPagar = await RepositorioReposiciones.ListarPorEstadoAsync(EstadoReposicion.Aprobada);

            await ResolverNombresDeUsuarioAsync(porAprobar.Select(s => s.FondoCajaChica?.CustodioId));
            await ResolverNombresDeUsuarioAsync(porPagar.Select(s => s.FondoCajaChica?.CustodioId));
        }

        private static decimal PorcentajeAlerta(FondoCajaChica fondo) =>
            fondo.PorcentajeAlertaReposicion > 0 ? fondo.PorcentajeAlertaReposicion : PorcentajeAlertaPorDefecto;

        private decimal Umbral => fondoActual is null ? 0m : fondoActual.MontoFijo * (PorcentajeAlerta(fondoActual) / 100m);

        private bool Habilitada => fondoActual is not null && fondoActual.BalanceActual <= Umbral;

        private decimal TotalPendientes => pendientes?.Sum(g => g.MontoTotal) ?? 0m;

        private decimal TotalPorPagar => porPagar?.Sum(s => s.MontoReclamado) ?? 0m;

        /// <summary>
        /// Único subtítulo que de verdad cambia con el estado del fondo: los otros
        /// tres roles trabajan por cola, no por fondo, así que su texto es fijo.
        /// </summary>
        private string SubtituloCustodio =>
            pendientes is not { Count: > 0 }
                ? "No hay gastos pendientes de reposición en este fondo."
                : Habilitada
                    ? "Su fondo ya bajó del umbral: puede mandar la solicitud."
                    : "Aún no baja del umbral necesario para solicitar la reposición.";

        private string Subtitulo =>
            puedeSolicitar ? SubtituloCustodio
            : puedeAprobar ? "Su bandeja primero; la consulta por fondo, más abajo."
            : puedePagar ? "Aprobadas listas para pagar. Al registrar el pago, el efectivo vuelve al fondo."
            : "Consulta de solo lectura: la traza completa de cada solicitud y su expediente.";

        /// <summary>
        /// La más antigua de la cola global de aprobación: ListarPorEstadoAsync ya
        /// ordena ascendente por FechaSolicitud, así que es simplemente la primera.
        /// </summary>
        private string DescripcionEsperaGerente
        {
            get
            {
                if (porAprobar is not { Count: > 0 })
                {
                    return string.Empty;
                }

                var dias = (int)(DateTime.UtcNow - porAprobar[0].FechaSolicitud).TotalDays;
                return dias switch
                {
                    <= 0 => "La más reciente llegó hoy",
                    1 => "La más antigua lleva 1 día esperando",
                    _ => $"La más antigua lleva {dias} días esperando"
                };
            }
        }

        private async Task CambiarFondoAsync(ChangeEventArgs e)
        {
            if (Guid.TryParse(e.Value?.ToString(), out var id))
            {
                errores.Clear();
                exito = null;
                fondoSeleccionado = id;
                await CargarFondoAsync(id);
            }
        }

        private async Task CargarFondoAsync(Guid fondoId)
        {
            pendientes = null;
            solicitudes = null;

            fondoActual = await Fondos.ObtenerPorIdAsync(fondoId);
            pendientes = await RepositorioGastos.ListarPendientesDeReposicionAsync(fondoId);
            solicitudes = await RepositorioReposiciones.ListarPorFondoAsync(fondoId);

            await ResolverNombresDeUsuarioAsync(solicitudes.SelectMany(s => new[] { s.GerenteUsuarioId, s.FinanzasUsuarioId }));
        }

        private async Task GenerarAsync()
        {
            errores.Clear();
            exito = null;
            generando = true;

            try
            {
                var resultado = await HandlerGenerar.EjecutarAsync(new CrearSolicitudReposicionCommand
                {
                    FondoCajaChicaId = fondoSeleccionado
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Solicitud creada. El expediente PDF ya está disponible para descargar.";
                await CargarFondoAsync(fondoSeleccionado);
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                generando = false;
            }
        }

        private async Task AprobarAsync(Guid reposicionId, bool aprobar)
        {
            errores.Clear();
            exito = null;
            procesando = reposicionId;

            try
            {
                var resultado = await HandlerAprobar.EjecutarAsync(new AprobarReposicionCommand
                {
                    ReposicionId = reposicionId,
                    Aprobar = aprobar
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = aprobar
                    ? "Solicitud aprobada."
                    : "Solicitud rechazada. Los gastos volvieron a quedar pendientes de reposición.";

                // Recargar es obligatorio, no cosmetico: el DbContext vive todo el
                // circuito y las listas apuntan a entidades ya rastreadas.
                await CargarColasAsync();

                if (fondos is { Count: > 0 })
                {
                    await CargarFondoAsync(fondoSeleccionado);
                }
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                procesando = null;
            }
        }

        private async Task PagarAsync(Guid reposicionId)
        {
            errores.Clear();
            exito = null;
            procesando = reposicionId;

            try
            {
                referencias.TryGetValue(reposicionId, out var referencia);

                var resultado = await HandlerPagar.EjecutarAsync(new ProcesarPagoReposicionCommand
                {
                    ReposicionId = reposicionId,
                    ReferenciaPago = referencia ?? string.Empty
                });

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                exito = "Pago registrado. El efectivo volvió al fondo.";
                referencias.Remove(reposicionId);

                await CargarColasAsync();

                if (fondos is { Count: > 0 })
                {
                    await CargarFondoAsync(fondoSeleccionado);
                }
            }
            catch (Exception ex)
            {
                errores.Add(ex.Message);
            }
            finally
            {
                procesando = null;
            }
        }

        // ── Historial: ventana de días + búsqueda ───────────────────────────────

        private void CambiarVentanaSolicitudes(ChangeEventArgs e)
        {
            var valor = e.Value?.ToString();
            diasSolicitudes = string.IsNullOrEmpty(valor) ? null : int.Parse(valor, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Filtra en memoria: ListarPorFondoAsync ya trae todas las solicitudes del
        /// fondo elegido de una sola vez, así que no hace falta volver a la BDD por
        /// cada cambio de ventana o cada tecla del buscador.
        /// </summary>
        private IEnumerable<SolicitudReposicion> SolicitudesFiltradas
        {
            get
            {
                if (solicitudes is null)
                {
                    return [];
                }

                var filtradas = solicitudes.AsEnumerable();

                if (diasSolicitudes is { } dias)
                {
                    var desde = DateTime.Today.AddDays(-dias);
                    filtradas = filtradas.Where(s => s.FechaSolicitud >= desde);
                }

                return string.IsNullOrWhiteSpace(busquedaSolicitudes)
                    ? filtradas
                    : filtradas.Where(s => Coincide(s, busquedaSolicitudes.Trim()));
            }
        }

        private bool Coincide(SolicitudReposicion solicitud, string busqueda)
        {
            var campos = new[]
            {
                solicitud.ReferenciaPago,
                CodigoCorto(solicitud.Id),
                solicitud.MontoReclamado.ToString("N2", CultureInfo.InvariantCulture),
                EtiquetaEstado(solicitud.Estado),
                NombreUsuario(solicitud.GerenteUsuarioId),
                NombreUsuario(solicitud.FinanzasUsuarioId)
            };

            return campos.Any(campo => campo is not null && ContieneSinAcentos(campo, busqueda));
        }

        private static bool ContieneSinAcentos(string texto, string busqueda) =>
            NormalizarParaBusqueda(texto).Contains(NormalizarParaBusqueda(busqueda), StringComparison.OrdinalIgnoreCase);

        private static string NormalizarParaBusqueda(string valor)
        {
            var normalizado = valor.Normalize(NormalizationForm.FormD);
            var sinDiacriticos = normalizado.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

            return new string(sinDiacriticos.ToArray()).Normalize(NormalizationForm.FormC);
        }

        private decimal TotalRepuesto =>
            SolicitudesFiltradas.Where(s => s.Estado == EstadoReposicion.Pagada).Sum(s => s.MontoReclamado);

        private static string CodigoCorto(Guid id) => id.ToString()[..8];

        // ── Estados y fechas ─────────────────────────────────────────────────────

        /// <summary>§5.7: píldora con fondo tenue, no la variante sólida de Bootstrap.</summary>
        private static string ClaseEstado(EstadoReposicion estado) => estado switch
        {
            EstadoReposicion.PendienteAprobacion => "rep-p-warn",
            EstadoReposicion.Aprobada => "rep-p-info",
            EstadoReposicion.Pagada => "rep-p-ok",
            EstadoReposicion.Rechazada => "rep-p-bad",
            _ => "rep-p-dark"
        };

        private static string EtiquetaEstado(EstadoReposicion estado) => estado switch
        {
            EstadoReposicion.PendienteAprobacion => "Pendiente de aprobación",
            EstadoReposicion.Aprobada => "Aprobada",
            EstadoReposicion.Pagada => "Pagada",
            EstadoReposicion.Rechazada => "Rechazada",
            _ => estado.ToString()
        };

        /// <summary>
        /// dd/MM/yyyy h:mm a.m./p.m. -- el mismo formato del artifact aprobado.
        /// CultureInfo.InvariantCulture y no una cultura es-* porque .NET renderiza
        /// "AM"/"PM" (o variantes con espacios) según la version del ICU del SO; se
        /// arma el sufijo a mano para que el resultado sea identico en cualquier
        /// maquina.
        /// </summary>
        private static string FormatoFechaHora(DateTime fechaUtc)
        {
            var local = fechaUtc.ToLocalTime();
            var sufijo = local.Hour < 12 ? "a.m." : "p.m.";
            return local.ToString("dd/MM/yyyy h:mm", CultureInfo.InvariantCulture) + " " + sufijo;
        }

        private static string FormatoFechaHoraOpcional(DateTime? fechaUtc) =>
            fechaUtc is { } valor ? FormatoFechaHora(valor) : "-";
    }
}
