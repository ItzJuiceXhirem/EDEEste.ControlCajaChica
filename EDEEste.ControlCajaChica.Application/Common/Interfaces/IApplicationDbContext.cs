using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Guarda y devuelve true; devuelve false si otro usuario ya habia modificado
        /// alguna de las filas involucradas.
        ///
        /// Existe porque la excepcion de concurrencia es un tipo de EF Core y esta
        /// capa no referencia paquetes, asi que la traduccion tiene que ocurrir del
        /// otro lado de la interfaz. Al fallar, descarta lo que quedo pendiente: en
        /// Blazor Server el contexto vive todo el circuito, y sin limpiarlo las
        /// entidades sucias se reintentarian en el siguiente clic del usuario.
        /// </summary>
        Task<bool> IntentarGuardarCambiosAsync(CancellationToken cancellationToken = default);
    }
}
