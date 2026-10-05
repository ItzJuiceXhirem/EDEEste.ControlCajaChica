using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IGastoRepository
    {
        Task<Gasto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Gasto>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default);

      /* Gastos que todavía no entraron en ninguna reposición. Trae los comprobantes
         incluidos porque son justo lo que necesita el PDF consolidado. */
        Task<IReadOnlyList<Gasto>> ListarPendientesDeReposicionAsync(Guid fondoId, CancellationToken cancellationToken = default);

        // Gastos del fondo en un estado dado. Alimenta la bandeja del Gerente (anulaciones por confirmar).
        Task<IReadOnlyList<Gasto>> ListarPorEstadoAsync(Guid fondoId, EstadoGasto estado, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gastos anulados del fondo. Con <paramref name="desde"/> en null trae el
        /// historial completo: los anulados no se purgan nunca, solo se acotan en pantalla.
        /// </summary>
        Task<IReadOnlyList<Gasto>> ListarAnuladosAsync(Guid fondoId, DateTime? desde, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gastos que todavía no volvieron al fondo: pendientes de reposición, en
        /// proceso de reposición o con anulación pendiente. Se enumeran en positivo y
        /// no por exclusión de Repuesto/Anulado, porque Rechazado hoy no lo escribe
        /// nadie y una lista negativa lo arrastraría dentro sin que nadie lo haya
        /// decidido. Alimenta el arqueo: BalanceActual + Σ(estos) debe cuadrar con
        /// MontoFijo.
        /// </summary>
        Task<IReadOnlyList<Gasto>> ListarNoRepuestosAsync(Guid fondoId, CancellationToken cancellationToken = default);

        Task AgregarAsync(Gasto gasto, CancellationToken cancellationToken = default);

      /* Un comprobante suelto, para el endpoint de descarga/visualización. No hace
         falta cargar el Gasto completo: la ruta física y el tipo MIME bastan para
         servir el archivo. */
        Task<ComprobanteAdjunto?> ObtenerComprobanteAsync(Guid comprobanteId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cuántos gastos usan esta categoría en el año dado, sin importar el fondo -
        /// una categoría es compartida por todos los fondos. Es el dato de contexto
        /// que ve el Administrador al editar una categoría ("Usada en X gastos este
        /// año"), no una lista que haya que materializar entera.
        /// </summary>
        Task<int> ContarPorCategoriaYAnioAsync(Guid categoriaGastoId, int anio, CancellationToken cancellationToken = default);
    }
}
