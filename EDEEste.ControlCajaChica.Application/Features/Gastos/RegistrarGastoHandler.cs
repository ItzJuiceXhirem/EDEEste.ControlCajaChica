using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
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
        /// <summary>
        /// Tope duro del README: ningun gasto puede superar el 2.5% del fondo fijo.
        /// El LimitePorGasto que configura el Administrador solo puede ser mas
        /// estricto que esto, nunca mas permisivo.
        /// </summary>
        private const decimal PorcentajeMaximoPorGasto = 0.025m;

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

        private readonly IFondoRepository _fondos;
        private readonly ICategoriaGastoRepository _categorias;
        private readonly IGastoRepository _gastos;
        private readonly IFileStorageService _almacenamiento;
        private readonly ICurrentUserService _usuarioActual;
        private readonly IApplicationDbContext _contexto;

        public RegistrarGastoHandler(
            IFondoRepository fondos,
            ICategoriaGastoRepository categorias,
            IGastoRepository gastos,
            IFileStorageService almacenamiento,
            ICurrentUserService usuarioActual,
            IApplicationDbContext contexto)
        {
            _fondos = fondos;
            _categorias = categorias;
            _gastos = gastos;
            _almacenamiento = almacenamiento;
            _usuarioActual = usuarioActual;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            RegistrarGastoCommand comando,
            CancellationToken cancellationToken = default)
        {
            var fondo = await _fondos.ObtenerPorIdAsync(comando.FondoCajaChicaId, cancellationToken);
            if (fondo is null)
            {
                return ResultadoOperacion<Guid>.Fallo("El fondo indicado no existe.");
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

            var usuario = await _usuarioActual.ObtenerAsync(cancellationToken);

            var gasto = new Gasto
            {
                FondoCajaChicaId = fondo.Id,
                CategoriaGastoId = categoria.Id,
                Proveedor = comando.Proveedor.Trim(),
                // Se guarda canonico (solo digitos): la columna es nvarchar(11) y con
                // los guiones de la cedula el texto mide 13. El formato es cosa de la
                // pantalla, no del dato.
                RNCProveedor = SoloDigitos(comando.RNCProveedor),
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

            if (categoria.RequiereNCF && string.IsNullOrWhiteSpace(comando.NCF))
            {
                errores.Add($"La categoria '{categoria.Nombre}' exige NCF.");
            }

            // DGII: NCF de papel = 11 caracteres, e-NCF electronico = 13.
            var ncf = comando.NCF?.Trim() ?? string.Empty;
            if (ncf.Length is not 0 and not 11 and not 13)
            {
                errores.Add("El NCF debe tener 11 caracteres (NCF) o 13 (e-NCF).");
            }

            // RNC de empresa = 9 digitos, cedula de persona fisica = 11. El formulario
            // muestra la cedula con guiones (XXX-XXXXXXX-X), asi que aqui se comparan
            // solo los digitos.
            var rnc = SoloDigitos(comando.RNCProveedor);
            if (rnc.Length > 0 && rnc.Length is not 9 and not 11)
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
        /// Deja solo los digitos de un RNC/cedula, descartando guiones y espacios.
        /// </summary>
        private static string SoloDigitos(string? valor) =>
            string.IsNullOrEmpty(valor)
                ? string.Empty
                : new string(valor.Where(char.IsDigit).ToArray());

        /// <summary>
        /// El limite efectivo es el mas estricto entre el 2.5% del fondo fijo (tope
        /// del README) y el LimitePorGasto configurado por el Administrador.
        /// </summary>
        private static decimal CalcularLimitePorGasto(FondoCajaChica fondo)
        {
            var topeReglamentario = fondo.MontoFijo * PorcentajeMaximoPorGasto;

            return fondo.LimitePorGasto > 0
                ? Math.Min(fondo.LimitePorGasto, topeReglamentario)
                : topeReglamentario;
        }
    }
}
