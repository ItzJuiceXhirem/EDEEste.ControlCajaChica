using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    /// <summary>
    /// Doble de IApplicationDbContext. No persiste nada por si mismo -- las pruebas
    /// verifican el estado de las mismas instancias que le pasaron al handler -- pero
    /// SI cuenta cuantas veces se guardo, que es la regla de la casa que ningun mock
    /// generico verificaria por accidente: "se guardo exactamente una vez".
    /// </summary>
    public sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public int VecesGuardado { get; private set; }

        /// <summary>Si true, la proxima llamada a IntentarGuardarCambiosAsync falla como si otro usuario ya hubiera modificado las filas.</summary>
        public bool FallarPorConcurrencia { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            VecesGuardado++;
            return Task.FromResult(0);
        }

        public Task<bool> IntentarGuardarCambiosAsync(CancellationToken cancellationToken = default)
        {
            if (FallarPorConcurrencia)
            {
                return Task.FromResult(false);
            }

            VecesGuardado++;
            return Task.FromResult(true);
        }
    }
}
