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

        /// <summary>Solo las marcadas como activas; es lo que se ofrece al registrar un gasto.</summary>
        Task<IReadOnlyList<CategoriaGasto>> ListarActivasAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// True si ya existe una categoria con ese nombre (comparacion insensible a
        /// mayusculas segun la intercalacion por defecto de SQL Server). Si se pasa
        /// <paramref name="excluirId"/>, esa categoria no cuenta -- es el caso de
        /// editar una categoria sin que choque contra si misma.
        /// </summary>
        Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId = null, CancellationToken cancellationToken = default);

        Task AgregarAsync(CategoriaGasto categoria, CancellationToken cancellationToken = default);
    }
}
