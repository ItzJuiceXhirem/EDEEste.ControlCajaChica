using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Nombres canonicos de los roles del sistema.
    /// ASP.NET Identity maneja los roles como texto, asi que estas constantes son
    /// la unica fuente de verdad: se usan para sembrar los roles al arrancar y en
    /// los atributos [Authorize(Roles = ...)], evitando cadenas magicas regadas.
    /// </summary>
    public static class RolesApp
    {
        /// <summary>Configura fondos, limites, categorias y asignacion de usuarios.</summary>
        public const string Administrador = "Administrador";

        /// <summary>Aprobador / Gerente de area: revisa gastos y aprueba reposiciones.</summary>
        public const string Gerente = "Gerente";

        /// <summary>Procesa el pago de la reposicion y la marca como pagada.</summary>
        public const string Finanzas = "Finanzas";

        /// <summary>Responsable del fondo: registra gastos, arquea y solicita reposiciones.</summary>
        public const string Custodio = "Custodio";

        /// <summary>Auditor interno: solo lectura sobre historial, arqueos y descuadres.</summary>
        public const string Auditor = "Auditor";

        /// <summary>
        /// Rol asignado a quien se registra por su cuenta. Es el de menor privilegio
        /// (solo lectura) a proposito: nadie debe poder auto-asignarse acceso al fondo.
        /// Un Administrador reasigna el rol real despues.
        /// </summary>
        public const string RolPorDefecto = Auditor;

        public static readonly IReadOnlyList<string> Todos =
        [
            Administrador,
            Gerente,
            Finanzas,
            Custodio,
            Auditor
        ];
    }
}
