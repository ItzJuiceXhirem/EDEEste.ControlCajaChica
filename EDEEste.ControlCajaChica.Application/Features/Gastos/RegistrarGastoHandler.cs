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
    /// Todo se confirma en un solo SaveChangesAsync: el gasto, sus comprobantes y el
    /// nuevo balance del fondo. Si algo falla, no queda un gasto registrado sin
    /// descontar (ni al reves). Los interceptores de auditoria e integridad se
    /// encargan solos de las fechas, el usuario y los sellos HMAC.
    /// </summary>
    public sealed class RegistrarGastoHandler
    {
        private static readonly HashSet<string> TiposMimePermitidos = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/jpg",
            "image/png"
        };

        private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png"
        };

        // 'B' + 0/1 + 9 digitos (NCF de papel, 11 caracteres) o 'E' + 3/4 + 11 digitos
        // (e-NCF, 13 caracteres). Nada mas pasa: ni espacios, ni guiones, ni otras letras.
        private static readonly Regex PatronNcf = new("^(B[01][0-9]{9}|E[34][0-9]{11})$", RegexOptions.Compiled);

        // Firmas (magic bytes) de los unicos cuatro tipos que TiposMimePermitidos
        // acepta. No hace falta una firma de PNG/JPEG separada por variante: los
        // primeros bytes ya identifican el formato sin importar el resto del archivo.
        private static readonly byte[] FirmaPdf = "%PDF"u8.ToArray();
        private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly byte[] FirmaJpeg = [0xFF, 0xD8, 0xFF];

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

            // Defensa en profundidad: la pantalla ya solo le ofrece al Custodio su
            // propio fondo en el desplegable, pero eso es filtrado de UI. Sin esta
            // comprobacion, un Custodio podria armar la peticion contra el circuito de
            // Blazor Server con el Id de otro fondo y descontarle el balance a otro
            // custodio.
            if (usuario.Id is { } usuarioIdRegistrar
                && await _identidad.EstaEnRolAsync(usuarioIdRegistrar, RolesApp.Custodio)
                && fondo.CustodioId != usuarioIdRegistrar)
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para registrar gastos en el fondo de otro custodio.");
            }

            var categoria = await _categorias.ObtenerPorIdAsync(comando.CategoriaGastoId, cancellationToken);
            if (categoria is null)
            {
                return ResultadoOperacion<Guid>.Fallo("La categoria indicada no existe.");
            }

            var errores = Validar(comando, fondo, categoria);

            // El TipoMime que llega aqui es el que reporta el navegador -- basta con
            // renombrar un archivo para que declare cualquier extension/MIME de la
            // lista blanca sin importar su contenido real. La firma (magic bytes) es
            // lo unico que no se puede spoofear con solo cambiar el nombre.
            foreach (var comprobante in comando.Comprobantes)
            {
                if (!await CoincideConFirmaEsperadaAsync(comprobante))
                {
                    errores.Add($"'{comprobante.NombreOriginal}' no coincide con su tipo declarado (contenido invalido).");
                }
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
                RegistradoPorUsuarioId = usuario.Id ?? string.Empty
            };

            foreach (var entrada in comando.Comprobantes)
            {
                var guardado = await _almacenamiento.GuardarComprobanteAsync(new SubirComprobanteDto
                {
                    NombreOriginal = entrada.NombreOriginal,
                    TipoMime = entrada.TipoMime,
                    TamanoBytes = entrada.TamanoBytes,
                    ContenidoArchivo = entrada.Contenido
                });

                gasto.Comprobantes.Add(new ComprobanteAdjunto
                {
                    GastoId = gasto.Id,
                    NombreOriginal = entrada.NombreOriginal,
                    Descripcion = entrada.Descripcion.Trim(),
                    RutaArchivo = guardado.RutaRelativa,
                    TipoMime = entrada.TipoMime,
                    TamanoBytes = entrada.TamanoBytes,
                    HashSHA256 = guardado.HashSha256,
                    FechaSubida = DateTime.UtcNow
                });
            }

            // El fondo baja por el monto del gasto: es lo que sostiene la invariante
            // "efectivo restante + gastos pendientes = fondo fijo".
            fondo.BalanceActual -= comando.MontoTotal;

            await _gastos.AgregarAsync(gasto, cancellationToken);
            await _contexto.SaveChangesAsync(cancellationToken);

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

            foreach (var comprobante in comando.Comprobantes)
            {
                var extension = Path.GetExtension(comprobante.NombreOriginal);

                // Se validan las dos cosas: el navegador reporta el MIME y es facil de
                // falsear, pero la extension es la que decide como se abre el archivo
                // despues.
                if (!TiposMimePermitidos.Contains(comprobante.TipoMime) || !ExtensionesPermitidas.Contains(extension))
                {
                    errores.Add($"'{comprobante.NombreOriginal}' no es un formato aceptado (solo PDF, JPG, JPEG o PNG).");
                }

                if (string.IsNullOrWhiteSpace(comprobante.Descripcion))
                {
                    errores.Add($"Falta la transcripcion de '{comprobante.NombreOriginal}'.");
                }
            }

            return errores;
        }

        /// <summary>
        /// Compara los primeros bytes del archivo contra la firma del tipo que declara
        /// (TipoMime, ya validado contra la lista blanca en Validar). Deja el stream
        /// en la posicion 0 al terminar: GuardarComprobanteAsync todavia necesita
        /// leerlo completo desde el principio.
        /// </summary>
        private static async Task<bool> CoincideConFirmaEsperadaAsync(RegistrarGastoCommand.ComprobanteEntrada comprobante)
        {
            var firma = comprobante.TipoMime.ToLowerInvariant() switch
            {
                "application/pdf" => FirmaPdf,
                "image/png" => FirmaPng,
                "image/jpeg" or "image/jpg" => FirmaJpeg,
                _ => null
            };

            // Un TipoMime fuera de la lista blanca ya lo rechaza Validar por su cuenta;
            // aqui no hay firma con la que comparar, asi que no se declara coincidencia.
            if (firma is null)
            {
                return false;
            }

            var buffer = new byte[firma.Length];
            comprobante.Contenido.Position = 0;
            var leidos = await comprobante.Contenido.ReadAsync(buffer.AsMemory(0, firma.Length));
            comprobante.Contenido.Position = 0;

            return leidos == firma.Length && buffer.AsSpan().SequenceEqual(firma);
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
