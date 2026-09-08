using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Constants;

namespace EDEEste.ControlCajaChica.Application.Features.Categorias
{
    public sealed class ActualizarCategoriaGastoHandler
    {
        private readonly ICategoriaGastoRepository _categorias;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public ActualizarCategoriaGastoHandler(
            ICategoriaGastoRepository categorias, IAutorizacionService autorizacion, IApplicationDbContext contexto)
        {
            _categorias = categorias;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            ActualizarCategoriaGastoCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.ConfigurarCategorias, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para configurar categorías.");
            }

            var categoria = await _categorias.ObtenerPorIdAsync(comando.CategoriaGastoId, cancellationToken);
            if (categoria is null)
            {
                return ResultadoOperacion<Guid>.Fallo("La categoria indicada no existe.");
            }

            var nombre = comando.Nombre?.Trim() ?? string.Empty;
            var nombreDuplicado = nombre.Length > 0
                && await _categorias.ExisteNombreAsync(nombre, comando.CategoriaGastoId, cancellationToken);

            var errores = Validar(comando, nombre, nombreDuplicado);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            categoria.Nombre = nombre;
            categoria.CuentaContable = comando.CuentaContable?.Trim() ?? string.Empty;
            categoria.RequiereNCF = comando.RequiereNCF;
            categoria.Activo = comando.Activo;

            // SaveChangesAsync liso y no IntentarGuardarCambiosAsync: a diferencia de
            // FondoCajaChica.BalanceActual/Gasto.Estado/SolicitudReposicion.Estado,
            // ninguna propiedad de CategoriaGasto esta marcada IsConcurrencyToken() en
            // ApplicationDbContext, asi que su UPDATE nunca lleva una clausula de
            // concurrencia -- DbUpdateConcurrencyException no puede ocurrir aca.
            await _contexto.SaveChangesAsync(cancellationToken);

            return ResultadoOperacion<Guid>.Ok(categoria.Id);
        }

        private static List<string> Validar(ActualizarCategoriaGastoCommand comando, string nombre, bool nombreDuplicado)
        {
            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                errores.Add("El nombre es obligatorio.");
            }
            else if (nombre.Length > LimitesCategoriaGasto.LongitudMaximaNombre)
            {
                errores.Add($"El nombre no puede superar {LimitesCategoriaGasto.LongitudMaximaNombre} caracteres.");
            }
            else if (nombreDuplicado)
            {
                errores.Add($"Ya existe una categoria llamada '{nombre}'.");
            }

            var cuentaContable = comando.CuentaContable?.Trim() ?? string.Empty;
            if (cuentaContable.Length > LimitesCategoriaGasto.LongitudMaximaCuentaContable)
            {
                errores.Add($"La cuenta contable no puede superar {LimitesCategoriaGasto.LongitudMaximaCuentaContable} caracteres.");
            }

            return errores;
        }
    }
}
