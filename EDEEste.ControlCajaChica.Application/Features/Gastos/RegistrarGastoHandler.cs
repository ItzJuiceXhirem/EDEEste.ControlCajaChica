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
    /// bajo IIS un archivo real choca con el limite de mensaje de SignalR y tumba
    /// el circuito entero. Este handler solo promueve (copia) esos archivos a su
    /// ubicacion final al confirmar el gasto.
    ///
    /// Todo se confirma en un solo SaveChangesAsync: el gasto, sus comprobantes y el
    /// nuevo balance del fondo. Si algo falla, no queda un gasto registrado sin
    /// descontar (ni al reves). Los interceptores de auditoria e integridad se
    /// encargan solos de las fechas, el usuario y los sellos HMAC.
    /// </summary>
    public sealed class RegistrarGastoHandler
    {
        // 'B' + 0/1 + 9 digitos (NCF de papel, 11 caracteres) o 'E' + 3/4 + 11 digitos
        // (e-NCF, 13 caracteres). Nada mas pasa: ni espacios, ni guiones, ni otras letras.
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
            // Defensa en profundidad: la pantalla ya exige este permiso, pero el
            // handler no debe depender solo de eso.
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

            // Ademas de identificar quien registra, usuarioId es la carpeta de staging
            // donde se buscan los comprobantes -- sin un Id resuelto no hay a donde ir
            // a buscarlos, asi que esto falla cerrado (a diferencia de la comprobacion
            // de dueño de mas abajo, que antes se saltaba entera si esto era null).
            if (usuario.Id is not { } usuarioId)
            {
                return ResultadoOperacion<Guid>.Fallo("No se pudo identificar al usuario actual.");
            }

            // Defensa en profundidad: la pantalla ya solo le ofrece al Custodio su
            // propio fondo en el desplegable, pero eso es filtrado de UI. Sin esta
            // comprobacion, un Custodio podria armar la peticion contra el circuito de
            // Blazor Server con el Id de otro fondo y descontarle el balance a otro
            // custodio.
            if (await _identidad.EstaEnRolAsync(usuarioId, RolesApp.Custodio) && fondo.CustodioId != usuarioId)
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para registrar gastos en el fondo de otro custodio.");
            }

            var categoria = await _categorias.ObtenerPorIdAsync(comando.CategoriaGastoId, cancellationToken);
            if (categoria is null)
            {
                return ResultadoOperacion<Guid>.Fallo("La categoria indicada no existe.");
            }

            var errores = Validar(comando, fondo, categoria);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            // Fase A: leer y validar TODOS los manifiestos de staging antes de
            // promover ninguno. El formato, el MIME y la firma del archivo ya se
            // comprobaron al subirlo (GastoEndpoints); esto solo confirma que la
            // referencia sigue siendo del usuario actual y sigue existiendo -- pudo
            // haberla descartado, o haberla alcanzado el barrido de staging.
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
                // Se guarda canonico (solo digitos, sin los guiones que la pantalla le
                // pone a la cedula): el formato es cosa de la pantalla, no del dato.
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

            // Fase B: promover (copiar, nunca mover) cada archivo a su ubicacion
            // final. Copiar y no mover es lo que hace que un fallo mas abajo (el mas
            // comun: un choque de concurrencia en el balance del fondo, no algo
            // exotico) no deje las referencias del usuario colgadas -- el original en
            // staging sigue intacto y el reintento no depende de volver a adjuntar
            // nada.
            foreach (var (entrada, manifiesto) in manifiestos)
            {
                var promovido = await _almacenamiento.PromoverComprobanteAsync(usuarioId, manifiesto, cancellationToken);

                // Se vuelve a comprobar la firma sobre el archivo YA copiado: el
                // contenido paso este mismo control al subirse, asi que esto solo
                // puede fallar si algo lo altero entre la subida y este guardado (por
                // ejemplo, escritura directa a App_Data). Los que ya se promovieron en
                // este mismo bucle quedan huerfanos en disco -- aceptable, el mismo
                // trade-off que CrearSolicitudReposicionHandler ya documenta: un
                // huerfano en disco esta bien, una fila que apunte a un archivo
                // invalido nunca lo esta.
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

            // El fondo baja por el monto del gasto: es lo que sostiene la invariante
            // "efectivo restante + gastos pendientes = fondo fijo".
            fondo.BalanceActual -= comando.MontoTotal;

            await _gastos.AgregarAsync(gasto, cancellationToken);

            // fondo.BalanceActual es token de concurrencia (ver ApplicationDbContext):
            // sin IntentarGuardarCambiosAsync, un choque real lanzaba
            // DbUpdateConcurrencyException sin traducir y dejaba el ChangeTracker
            // sucio -- en Blazor Server el contexto vive todo el circuito, asi que el
            // siguiente clic del usuario reintentaria este mismo guardado fallido.
            if (!await _contexto.IntentarGuardarCambiosAsync(cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo(
                    "Otro usuario modificó este fondo mientras usted trabajaba. Recargue la pantalla e intente de nuevo.");
            }

            // Los comprobantes ya quedaron a salvo en su ubicacion final y la fila del
            // gasto se guardo con exito: recien ahora se descarta el original de
            // staging. Best-effort -- si esto fallara, el barrido de staging lo limpia
            // igual mas tarde, y no hay ninguna fila que dependa de que suceda ahora.
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
                errores.Add("El fondo no esta activo, no admite gastos nuevos.");
            }

            if (comando.MontoTotal <= 0)
            {
                errores.Add("El monto total debe ser mayor que cero.");
            }

            // Sin esto, un subtotal negativo compensado con un ITBIS inflado cuadra
            // contra el total y pasa el resto de las validaciones. El formulario ya
            // pone min="0", pero eso es del navegador: la regla tiene que vivir aqui.
            if (comando.Subtotal < 0)
            {
                errores.Add("El subtotal no puede ser negativo.");
            }

            if (comando.MontoITBIS < 0)
            {
                errores.Add("El ITBIS no puede ser negativo.");
            }

            // El ITBIS es un porcentaje del subtotal (18% salvo articulos exentos), asi
            // que nunca puede igualarlo ni superarlo; si eso pasa, alguien invirtio los
            // campos o escribio un monto que no corresponde a ningun impuesto real.
            if (comando.MontoITBIS >= comando.Subtotal)
            {
                errores.Add("El ITBIS debe ser menor que el subtotal.");
            }

            // Se compara el desglose contra el total en vez de calcularlo, porque el
            // ITBIS que aparece impreso en la factura manda sobre cualquier cuenta
            // nuestra (hay articulos exentos y tasas distintas).
            if (comando.Subtotal + comando.MontoITBIS != comando.MontoTotal)
            {
                errores.Add("El subtotal mas el ITBIS no cuadra con el monto total de la factura.");
            }

            var limite = CalcularLimitePorGasto(fondo);
            if (comando.MontoTotal > limite)
            {
                errores.Add($"El gasto de RD$ {comando.MontoTotal:N2} supera el limite por gasto de RD$ {limite:N2}.");
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
                errores.Add($"La categoria '{categoria.Nombre}' exige NCF.");
            }

            // DGII: NCF de papel es 'B' + 0/1 + 9 digitos (11 caracteres); e-NCF
            // electronico es 'E' + 3/4 + 11 digitos (13 caracteres). Nada de espacios,
            // guiones ni otras letras -- si no calza con ninguno de los dos moldes, se
            // rechaza entero en vez de aceptar lo que se pueda.
            if (ncf.Length > 0 && !PatronNcf.IsMatch(ncf))
            {
                errores.Add(
                    "El NCF no es valido. Debe empezar con 'B' seguido de 0 o 1 y 9 digitos mas " +
                    "(11 caracteres), o con 'E' seguido de 3 o 4 y 11 digitos mas (13 caracteres). " +
                    "No se permiten espacios, guiones ni otras letras.");
            }

            // El unico caracter no numerico que se tolera es el guion que la pantalla
            // le agrega a la cedula (XXX-XXXXXXX-X); cualquier otra cosa -- letras,
            // espacios, otros simbolos -- se rechaza entera en vez de descartarla en
            // silencio, que es lo que pasaba antes: un RNC con letras de relleno podia
            // "cuadrar" en 9 u 11 digitos por accidente una vez limpiado.
            var rnc = SinGuiones(comando.RNCProveedor);
            if (rnc.Length > 0 && !rnc.All(char.IsDigit))
            {
                errores.Add("El RNC/Cedula solo puede contener numeros (los guiones de la cedula se aceptan aparte).");
            }
            else if (rnc.Length > 0 && rnc.Length is not 9 and not 11)
            {
                errores.Add("El RNC debe tener 9 digitos (empresa) u 11 (cedula).");
            }

            if (comando.Comprobantes.Count == 0)
            {
                errores.Add("Debe adjuntar al menos un comprobante.");
            }

            if (comando.Comprobantes.Count > LimitesGasto.ComprobantesPorGasto)
            {
                errores.Add($"No se pueden adjuntar mas de {LimitesGasto.ComprobantesPorGasto} comprobantes.");
            }

            // El formato/MIME/firma del archivo ya se comprobaron al subirlo a
            // staging (GastoEndpoints); aqui solo queda la transcripcion, que es un
            // dato que el usuario escribe en esta misma pantalla.
            for (var i = 0; i < comando.Comprobantes.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(comando.Comprobantes[i].Descripcion))
                {
                    errores.Add($"Falta la transcripcion del comprobante #{i + 1}.");
                }
            }

            return errores;
        }

        /// <summary>
        /// Quita solo guiones (el unico caracter de formato que la pantalla agrega a
        /// la cedula). No toca ningun otro caracter: si quedo una letra o un simbolo,
        /// tiene que seguir ahi para que la validacion de "solo numeros" lo detecte.
        /// </summary>
        private static string SinGuiones(string? valor) =>
            string.IsNullOrEmpty(valor) ? string.Empty : valor.Trim().Replace("-", string.Empty);

        /// <summary>
        /// El limite efectivo es el mas estricto entre el tope porcentual que el
        /// Administrador fijo para este fondo (2.5% por defecto, el valor del README)
        /// y el LimitePorGasto absoluto, si lo definio.
        ///
        /// Son dos perillas distintas a proposito: el porcentaje escala con el fondo,
        /// mientras que el limite en pesos no se mueve aunque el fondo crezca.
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
