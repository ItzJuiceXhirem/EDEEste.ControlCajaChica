using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /* Nombres canónicos de los roles del sistema.
       ASP.NET Identity maneja los roles como texto, así que estas constantes son
       la única fuente de verdad: se usan para sembrar los roles al arrancar y en
       los atributos [Authorize(Roles = ...)], evitando cadenas mágicas regadas */
    public static class RolesApp
    {
        // Configura fondos, límites, categorías y asignación de usuarios
        public const string Administrador = "Administrador";

        // Aprobador / Gerente de área: revisa gastos y aprueba reposiciones
        public const string Gerente = "Gerente";

        // Procesa el pago de la reposición y la marca como pagada
        public const string Finanzas = "Finanzas";

        // Responsable del fondo: registra gastos, arquea y solicita reposiciones
        public const string Custodio = "Custodio";

        // Auditor interno: solo lectura sobre historial, arqueos y descuadres
        public const string Auditor = "Auditor";

        /* Rol asignado a quien se registra por su cuenta. Es el de menor privilegio
           (solo lectura) a propósito: nadie debe poder auto-asignarse acceso al fondo.
           Un Administrador reasigna el rol real después */
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
