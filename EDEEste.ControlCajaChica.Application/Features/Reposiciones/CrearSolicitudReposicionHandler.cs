using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Arma la solicitud de reposicion con los gastos pendientes del fondo, genera el
    /// expediente PDF consolidado y deja los gastos marcados como en proceso para que
    /// no entren en una segunda reposicion.
    /// </summary>
    public sealed class CrearSolicitudReposicionHandler
    {
        private readonly IFondoRepository _fondos;
        private readonly IGastoRepository _gastos;
        private readonly IReposicionRepository _reposiciones;
        private readonly IPdfConsolidadorService _consolidador;
        private readonly IFileStorageService _almacenamiento;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IIdentityService _identidad;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public CrearSolicitudReposicionHandler(
            IFondoRepository fondos,
            IGastoRepository gastos,
            IReposicionRepository reposiciones,
            IPdfConsolidadorService consolidador,
            IFileStorageService almacenamiento,
            ICurrentUserService usuarioActual,
            IIdentityService identidad,
            IAutorizacionService autorizacion,
            IApplicationDbContext contexto)
        {
            _fondos = fondos;
            _gastos = gastos;
            _reposiciones = reposiciones;
            _consolidador = consolidador;
            _almacenamiento = almacenamiento;
            _usuarioActual = usuarioActual;
            _identidad = identidad;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            CrearSolicitudReposicionCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.SolicitarReposicion, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para solicitar una reposición.");
            }

            var fondo = await _fondos.ObtenerPorIdAsync(comando.FondoCajaChicaId, cancellationToken);
            if (fondo is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El fondo indicado no existe.");
            }

            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);

            // El Id resuelto tambien queda como SolicitoUsuarioId mas abajo -- sin
            // poder identificarlo, esto falla cerrado en vez de dejar un registro con
            // el campo vacio (y sin poder verificar tampoco que el fondo sea el suyo).
            if (usuario.Id is not { } usuarioIdSolicitar)
            {
                return ResultadoOperacion<Guid>.Fallo("No se pudo identificar al usuario actual.");
            }

            // Defensa en profundidad: sin esto, un Custodio podria solicitar la
            // reposicion del fondo de otro custodio armando la peticion contra el
            // circuito de Blazor Server, aunque la pantalla ya solo le ofrezca el suyo.
            if (await _identidad.EstaEnRolAsync(usuarioIdSolicitar, RolesApp.Custodio) && fondo.CustodioId != usuarioIdSolicitar)
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para solicitar la reposicion del fondo de otro custodio.");
            }

            var pendientes = await _gastos.ListarPendientesDeReposicionAsync(fondo.Id, cancellationToken);
            if (pendientes.Count == 0)
            {
                return ResultadoOperacion<Guid>.Fallo("El fondo no tiene gastos pendientes de reposicion.");
            }

            var montoReclamado = pendientes.Sum(g => g.MontoTotal);

            var errores = Validar(fondo, montoReclamado);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            var solicitud = new SolicitudReposicion
            {
                FondoCajaChicaId = fondo.Id,
                FondoCajaChica = fondo,
                MontoReclamado = montoReclamado,
                FechaSolicitud = DateTime.UtcNow,
                SolicitoUsuarioId = usuarioIdSolicitar,
                Estado = EstadoReposicion.PendienteAprobacion
            };

            foreach (var gasto in pendientes)
            {
                gasto.ReposicionId = solicitud.Id;
                gasto.Estado = EstadoGasto.EnProcesoReposicion;
                solicitud.Gastos.Add(gasto);
            }

            // El PDF se genera antes de guardar para poder dejar RutaPdfConsolidado ya
            // resuelta en el mismo INSERT. Si el guardado en BDD fallara despues, el
            // archivo quedaria huerfano en disco, pero nunca al reves (una solicitud
            // apuntando a un PDF que no existe).
            var pdf = await _consolidador.ConsolidarComprobantesAsync(solicitud, cancellationToken);
            var archivo = await _almacenamiento.GuardarPdfConsolidadoAsync(
                pdf,
                $"reposicion-{solicitud.Id}.pdf");

            solicitud.RutaPdfConsolidado = archivo.RutaRelativa;

            await _reposiciones.AgregarAsync(solicitud, cancellationToken);

            // gasto.Estado es token de concurrencia (ver ApplicationDbContext): sin
            // IntentarGuardarCambiosAsync, un choque real (por ejemplo dos solicitudes
            // armadas a la vez sobre los mismos gastos pendientes) lanzaba
            // DbUpdateConcurrencyException sin traducir y dejaba el ChangeTracker
            // sucio para el resto del circuito de Blazor Server.
            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modificó los gastos de este fondo mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            return ResultadoOperacion<Guid>.Ok(solicitud.Id);
        }

        private static List<string> Validar(FondoCajaChica fondo, decimal montoReclamado)
        {
            var errores = new List<string>();

            if (fondo.Estado != EstadoFondo.Activo)
            {
                errores.Add("El fondo no esta activo.");
            }

            // "Validar que lo que se vaya a reposicionar no sea mas que el fondo fijo
            // inicial": si esto se dispara, hay gastos que nunca debieron aprobarse.
            if (montoReclamado > fondo.MontoFijo)
            {
                errores.Add(
                    $"El monto a reponer (RD$ {montoReclamado:N2}) supera el fondo fijo de RD$ {fondo.MontoFijo:N2}.");
            }

            var porcentajeAlerta = fondo.PorcentajeAlertaReposicion > 0
                ? fondo.PorcentajeAlertaReposicion
                : LimitesFondo.AlertaReposicionPorDefecto;

            var umbral = fondo.MontoFijo * (porcentajeAlerta / 100m);
            if (fondo.BalanceActual > umbral)
            {
                errores.Add(
                    $"Todavia queda RD$ {fondo.BalanceActual:N2} en el fondo. La reposicion se habilita al bajar " +
                    $"de RD$ {umbral:N2} ({porcentajeAlerta:N0}% del fondo fijo).");
            }

            return errores;
        }
    }
}
