using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface ICategoriaGastoRepository
    {
        Task<CategoriaGasto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<CategoriaGasto>> ListarAsync(CancellationToken cancellationToken = default);

        // Solo las marcadas como activas; es lo que se ofrece al registrar un gasto
        Task<IReadOnlyList<CategoriaGasto>> ListarActivasAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// True si ya existe una categoría con ese nombre (comparacion insensible a
        /// mayusculas según la intercalación por defecto de SQL Server). Si se pasa
        /// <paramref name="excluirId"/>, esa categoría no cuenta -- es el caso de
        /// editar una categorií sin que choque contra sí misma.
        /// </summary>
        Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId = null, CancellationToken cancellationToken = default);

        Task AgregarAsync(CategoriaGasto categoria, CancellationToken cancellationToken = default);
    }
}
