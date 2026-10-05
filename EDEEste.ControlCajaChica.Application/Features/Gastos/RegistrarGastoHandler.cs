using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    /// <summary>
    /// Registra un gasto con sus comprobantes y descuenta el monto del fondo.
    ///
    /// Los comprobantes llegan como referencias a archivos ya subidos a staging
    /// (ver GastoEndpoints, POST /gastos/comprobantes/staging) y no como streams:
    /// la subida va por HTTP normal, fuera del circuito de Blazor Server, porque
    /// bajo IIS un archivo real choca con el límite de mensaje de SignalR y tumba
    /// el circuito entero. Este handler solo promueve (copia) esos archivos a su
    /// ubicación final al confirmar el gasto.
    ///
    /// Todo se confirma en un solo SaveChangesAsync: el gasto, sus comprobantes y el
    /// nuevo balance del fondo. Si algo falla, no queda un gasto registrado sin
    /// descontar (ni al revés). Los interceptores de auditoría e integridad se
    /// encargan solos de las fechas, el usuario y los sellos HMAC.
    /// </summary>
    public sealed class RegistrarGastoHandler
    {
        /* 'B' + 0/1 + 9 dígitos (NCF de papel, 11 caracteres) o 'E' + 3/4 + 11 dígitos
           (e-NCF, 13 caracteres). Este es el formato real en República Dominicana. Nada
           más pasa: ni espacios, ni guiones, ni otras letras. */
        private static readonly Regex PatronNcf = new("^(B[01][0-9]{9}|E[34][0-9]{11})$", RegexOptions.Compiled);

        private readonly IFondoRepository _fondos;
        private readonly ICategoriaGastoRepository _categorias;
        private readonly IGastoRepository _gastos;
        private readonly IFileStorageService _almacenamiento;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IIdentityService _identidad;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public RegistrarGastoHandler(
            IFondoRepository fondos,
            ICategoriaGastoRepository categorias,
            IGastoRepository gastos,
            IFileStorageService almacenamiento,
            ICurrentUserService usuarioActual,
            IIdentityService identidad,
            IAutorizacionService autorizacion,
            IApplicationDbContext contexto)
        {
            _fondos = fondos;
            _categorias = categorias;
            _gastos = gastos;
            _almacenamiento = almacenamiento;
            _usuarioActual = usuarioActual;
            _identidad = identidad;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            RegistrarGastoCommand comando,
            CancellationToken cancellationToken = default)
        {
          /* Defensa en profundidad: la pantalla ya exige este permiso, pero el
             handler no debe depender solo de eso. */
            if (!await _autorizacion.TienePermisoAsync(Permisos.RegistrarGasto, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para registrar gastos.");
            }

            var fondo = await _fondos.ObtenerPorIdAsync(comando.FondoCajaChicaId, cancellationToken);
            if (fondo is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El fondo indicado no existe.");
            }

            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);

          /* Además de identificar quién registra, usuarioId es la carpeta de staging
             donde se buscan los comprobantes -- sin un Id resuelto no hay a donde ir
             a buscarlos, así que esto falla cerrado (a diferencia de la comprobación
             de dueño de más abajo, que antes se saltaba entera si esto era null). */
            if (usuario.Id is not { } usuarioId)
            {
                return ResultadoOperacion<Guid>.Fallo("No se pudo identificar al usuario actual.");
            }

          /* Defensa en profundidad: la pantalla ya solo le ofrece al Custodio su
             propio fondo en el desplegable, pero eso es filtrado de UI. Sin esta
             comprobación, un Custodio podría armar la petición contra el circuito de
             Blazor Server con el Id de otro fondo y descontarle el balance a otro
             custodio. */
            if (await _identidad.EstaEnRolAsync(usuarioId, RolesApp.Custodio) && fondo.CustodioId != usuarioId)
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para registrar gastos en el fondo de otro custodio.");
            }

            var categoria = await _categorias.ObtenerPorIdAsync(comando.CategoriaGastoId, cancellationToken);
            if (categoria is null)
            {
                return ResultadoOperacion<Guid>.Fallo("La categoría indicada no existe.");
            }

            var errores = Validar(comando, fondo, categoria);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

          /* Fase A: leer y validar TODOS los manifiestos de staging antes de
             promover ninguno. El formato, el MIME y la firma del archivo ya se
             comprobaron al subirlo (GastoEndpoints); esto solo confirma que la
             referencia sigue siendo del usuario actual y sigue existiendo -- pudo
             haberla descartado, o haberla alcanzado el barrido de staging. */
            var manifiestos = new List<(RegistrarGastoCommand.ComprobanteEntrada Entrada, ComprobanteStagingDto Manifiesto)>();
            foreach (var entrada in comando.Comprobantes)
            {
                var manifiesto = await _almacenamiento.LeerManifiestoStagingAsync(usuarioId, entrada.Referencia, cancellationToken);
                if (manifiesto is null)
                {
                    errores.Add($"El comprobante '{entrada.Descripcion}' ya no está disponible; vuelva a adjuntarlo.");
                    continue;
                }

                manifiestos.Add((entrada, manifiesto));
            }

            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            var gasto = new Gasto
            {
                FondoCajaChicaId = fondo.Id,
                CategoriaGastoId = categoria.Id,
                Proveedor = comando.Proveedor.Trim(),
              /* Se guarda canónico (solo dígitos, sin los guiones que la pantalla le
                 pone a la cédula): el formato es cosa de la pantalla, no del dato. */
                RNCProveedor = SinGuiones(comando.RNCProveedor),
                NCF = comando.NCF.Trim(),
                Concepto = comando.Concepto?.Trim(),
                Subtotal = comando.Subtotal,
                MontoITBIS = comando.MontoITBIS,
                MontoTotal = comando.MontoTotal,
                FechaGasto = comando.FechaGasto,
                Estado = EstadoGasto.PendienteReposicion,
                RegistradoPorUsuarioId = usuarioId
            };

            /* Fase B: promover (copiar, nunca mover) cada archivo a su ubicación
               final. Copiar y no mover es lo que hace que un fallo más abajo (el más
               común: un choque de concurrencia en el balance del fondo, no algo
               exótico) no deje las referencias del usuario colgadas -- el original en
               staging sigue intacto y el reintento no depende de volver a adjuntar nada. */

            foreach (var (entrada, manifiesto) in manifiestos)
            {
                var promovido = await _almacenamiento.PromoverComprobanteAsync(usuarioId, manifiesto, cancellationToken);

              /* Se vuelve a comprobar la firma sobre el archivo YA copiado: el
                 contenido pasó este mismo control al subirse, así que esto solo
                 puede fallar si algo lo alteró entre la subida y este guardado (por
                 ejemplo, escritura directa a App_Data). Los que ya se promovieron en
                 este mismo bucle quedan huérfanos en disco -- aceptable, el mismo
                 trade-off que CrearSolicitudReposicionHandler ya documenta: un
                 huérfano en disco está bien, una fila que apunte a un archivo
                 inválido nunca lo está. */
                var bytesFinal = await _almacenamiento.LeerArchivoAsync(promovido.RutaRelativa);
                if (bytesFinal is null
                    || !await ValidadorComprobante.CoincideConFirmaEsperadaAsync(
                        new MemoryStream(bytesFinal), promovido.TipoMime, cancellationToken))
                {
                    errores.Add($"El comprobante '{entrada.Descripcion}' no pasó la verificación de contenido.");
                    continue;
                }

                gasto.Comprobantes.Add(new ComprobanteAdjunto
                {
                    GastoId = gasto.Id,
                    NombreOriginal = promovido.NombreOriginal,
                    Descripcion = entrada.Descripcion.Trim(),
                    RutaArchivo = promovido.RutaRelativa,
                    TipoMime = promovido.TipoMime,
                    TamanoBytes = promovido.TamanoBytes,
                    HashSHA256 = promovido.HashSha256,
                    FechaSubida = DateTime.UtcNow
                });
            }

            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

          /* El fondo baja por el monto del gasto: es lo que sostiene la invariante
             "efectivo restante + gastos pendientes = fondo fijo". */
            fondo.BalanceActual -= comando.MontoTotal;

            await _gastos.AgregarAsync(gasto, cancellationToken);

          /* fondo.BalanceActual es token de concurrencia (ver ApplicationDbContext):
             sin IntentarGuardarCambiosAsync, un choque real lanzaba
             DbUpdateConcurrencyException sin traducir y dejaba el ChangeTracker
             sucio -- en Blazor Server el contexto vive todo el circuito, así que el
             siguiente clic del usuario reintentaria este mismo guardado fallido. */
            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modificó este fondo mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

          /* Los comprobantes ya quedaron a salvo en su ubicación final y la fila del
             gasto se guardó con éxito: recién ahora se descarta el original de
             staging. Best-effort -- si esto fallara, el barrido de staging lo limpia
             igual más tarde, y no hay ninguna fila que dependa de que suceda ahora. */
            foreach (var (entrada, _) in manifiestos)
            {
                await _almacenamiento.EliminarStagingAsync(usuarioId, entrada.Referencia, cancellationToken);
            }

            return ResultadoOperacion<Guid>.Ok(gasto.Id);
        }

        private static List<string> Validar(RegistrarGastoCommand comando, FondoCajaChica fondo, CategoriaGasto categoria)
        {
            var errores = new List<string>();

            if (fondo.Estado != EstadoFondo.Activo)
            {
                errores.Add("El fondo no está activo, no admite gastos nuevos.");
            }

            if (comando.MontoTotal <= 0)
            {
                errores.Add("El monto total debe ser mayor que cero.");
            }

          /* Sin esto, un subtotal negativo compensado con un ITBIS inflado cuadra
             contra el total y pasa el resto de las validaciones. El formulario ya
             pone min="0", pero eso es del navegador: la regla tiene que vivir aquí. */
            if (comando.Subtotal < 0)
            {
                errores.Add("El subtotal no puede ser negativo.");
            }

            if (comando.MontoITBIS < 0)
            {
                errores.Add("El ITBIS no puede ser negativo.");
            }

          /* El ITBIS es un porcentaje del subtotal (18% salvo articulos exentos), así
             que nunca puede igualarlo ni superarlo; si eso pasa, alguien invirtió los
             campos o escribió un monto que no corresponde a ningún impuesto real. */
            if (comando.MontoITBIS >= comando.Subtotal)
            {
                errores.Add("El ITBIS debe ser menor que el subtotal.");
            }

          /* Se compara el desglose contra el total en vez de calcularlo, porque el
             ITBIS que aparece impreso en la factura manda sobre cualquier cuenta
             nuestra (hay artículos exentos y tasas distintas). */
            if (comando.Subtotal + comando.MontoITBIS != comando.MontoTotal)
            {
                errores.Add("El subtotal más el ITBIS no cuadra con el monto total de la factura.");
            }

            var limite = CalcularLimitePorGasto(fondo);
            if (comando.MontoTotal > limite)
            {
                errores.Add($"El gasto de RD$ {comando.MontoTotal:N2} supera el límite por gasto de RD$ {limite:N2}.");
            }

            if (comando.MontoTotal > fondo.BalanceActual)
            {
                errores.Add($"El fondo solo tiene RD$ {fondo.BalanceActual:N2} disponibles.");
            }

            if (comando.FechaGasto.Date > DateTime.Today)
            {
                errores.Add("La fecha del gasto no puede ser futura.");
            }

            if (string.IsNullOrWhiteSpace(comando.Proveedor))
            {
                errores.Add("Debe indicar el proveedor.");
            }

            var ncf = comando.NCF?.Trim() ?? string.Empty;

            if (categoria.RequiereNCF && ncf.Length == 0)
            {
                errores.Add($"La categoría '{categoria.Nombre}' exige NCF.");
            }

          /* DGII: NCF de papel es 'B' + 0/1 + 9 dígitos (11 caracteres); e-NCF
             electrónico es 'E' + 3/4 + 11 digitos (13 caracteres). Nada de espacios,
             guiones ni otras letras -- si no calza con ninguno de los dos moldes, se
             rechaza entero en vez de aceptar lo que se pueda. */
            if (ncf.Length > 0 && !PatronNcf.IsMatch(ncf))
            {
                errores.Add(
                    "El NCF no es válido. Debe empezar con 'B' seguido de 0 o 1 y 9 dígitos más " +
                    "(11 caracteres), o con 'E' seguido de 3 o 4 y 11 dígitos más (13 caracteres). " +
                    "No se permiten espacios, guiones ni otras letras.");
            }

          /* El único caracter no numérico que se tolera es el guión que la pantalla
             le agrega a la cédula (XXX-XXXXXXX-X); cualquier otra cosa -- letras,
             espacios, otros símbolos -- se rechaza entera en vez de descartarla en
             silencio, que es lo que pasaba antes: un RNC con letras de relleno podía
             "cuadrar" en 9 u 11 dígitos por accidente una vez limpiado. */
            var rnc = SinGuiones(comando.RNCProveedor);
            if (rnc.Length > 0 && !rnc.All(char.IsDigit))
            {
                errores.Add("El RNC/Cédula solo puede contener números (los guiones de la cédula se aceptan aparte).");
            }
            else if (rnc.Length > 0 && rnc.Length is not 9 and not 11)
            {
                errores.Add("El RNC debe tener 9 dígitos (empresa) u 11 (cédula).");
            }

            if (comando.Comprobantes.Count == 0)
            {
                errores.Add("Debe adjuntar al menos un comprobante.");
            }

            if (comando.Comprobantes.Count > LimitesGasto.ComprobantesPorGasto)
            {
                errores.Add($"No se pueden adjuntar más de {LimitesGasto.ComprobantesPorGasto} comprobantes.");
            }

          /* El formato/MIME/firma del archivo ya se comprobaron al subirlo a
             staging (GastoEndpoints); aquí solo queda la transcripción, que es un
             dato que el usuario escribe en esta misma pantalla. */
            for (var i = 0; i < comando.Comprobantes.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(comando.Comprobantes[i].Descripcion))
                {
                    errores.Add($"Falta la transcripción del comprobante #{i + 1}.");
                }
            }

            return errores;
        }

        /// <summary>
        /// Quita solo guiones (el unico caracter de formato que la pantalla agrega a
        /// la cedula). No toca ningún otro caracter: si quedo una letra o un símbolo,
        /// tiene que seguir ahí para que la validación de "solo números" lo detecte.
        /// </summary>
        private static string SinGuiones(string? valor) =>
            string.IsNullOrEmpty(valor) ? string.Empty : valor.Trim().Replace("-", string.Empty);

        /// <summary>
        /// El límite efectivo es el más estricto entre el tope porcentual que el
        /// Administrador fijó para este fondo (2.5% por defecto, el valor del README)
        /// y el LimitePorGasto absoluto, si lo definió.
        ///
        /// Son dos perillas distintas a propósito: el porcentaje escala con el fondo,      LÓGICA DE LÍMITES DE FONDO DIFERENTES
        /// mientras que el limite en pesos no se mueve aunque el fondo crezca.            <--------------------------------------
        /// </summary>
        private static decimal CalcularLimitePorGasto(FondoCajaChica fondo)
        {
            var topeReglamentario = fondo.MontoFijo * (fondo.PorcentajeMaximoPorGasto / 100m);

            return fondo.LimitePorGasto > 0
                ? Math.Min(fondo.LimitePorGasto, topeReglamentario)
                : topeReglamentario;
        }
    }
}
