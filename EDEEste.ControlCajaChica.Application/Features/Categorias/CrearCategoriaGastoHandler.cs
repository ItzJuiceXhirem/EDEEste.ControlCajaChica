using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Features.Categorias
{
    public sealed class CrearCategoriaGastoHandler
    {
        private const int LongitudMaximaNombre = 100;
        private const int LongitudMaximaCuentaContable = 50;

        private readonly ICategoriaGastoRepository _categorias;
        private readonly IAutorizacionService _autorizacion;
        private readonly IApplicationDbContext _contexto;

        public CrearCategoriaGastoHandler(
            ICategoriaGastoRepository categorias, IAutorizacionService autorizacion, IApplicationDbContext contexto)
        {
            _categorias = categorias;
            _autorizacion = autorizacion;
            _contexto = contexto;
        }

        public async Task<ResultadoOperacion<Guid>> EjecutarAsync(
            CrearCategoriaGastoCommand comando,
            CancellationToken cancellationToken = default)
        {
            if (!await _autorizacion.TienePermisoAsync(Permisos.ConfigurarCategorias, cancellationToken))
            {
                return ResultadoOperacion<Guid>.Fallo("No tiene permiso para configurar categorías.");
            }

            var nombre = comando.Nombre?.Trim() ?? string.Empty;
            var nombreDuplicado = nombre.Length > 0
                && await _categorias.ExisteNombreAsync(nombre, cancellationToken: cancellationToken);

            var errores = Validar(comando, nombre, nombreDuplicado);
            if (errores.Count > 0)
            {
                return ResultadoOperacion<Guid>.Fallo(errores);
            }

            var categoria = new CategoriaGasto
            {
                Nombre = nombre,
                CuentaContable = comando.CuentaContable?.Trim() ?? string.Empty,
                RequiereNCF = comando.RequiereNCF,
                Activo = comando.Activo
            };

            await _categorias.AgregarAsync(categoria, cancellationToken);
            await _contexto.SaveChangesAsync(cancellationToken);

            return ResultadoOperacion<Guid>.Ok(categoria.Id);
        }

        private static List<string> Validar(CrearCategoriaGastoCommand comando, string nombre, bool nombreDuplicado)
        {
            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                errores.Add("El nombre es obligatorio.");
            }
            else if (nombre.Length > LongitudMaximaNombre)
            {
                errores.Add($"El nombre no puede superar {LongitudMaximaNombre} caracteres.");
            }
            else if (nombreDuplicado)
            {
                errores.Add($"Ya existe una categoria llamada '{nombre}'.");
            }

            var cuentaContable = comando.CuentaContable?.Trim() ?? string.Empty;
            if (cuentaContable.Length > LongitudMaximaCuentaContable)
            {
                errores.Add($"La cuenta contable no puede superar {LongitudMaximaCuentaContable} caracteres.");
            }

            return errores;
        }
    }
}
