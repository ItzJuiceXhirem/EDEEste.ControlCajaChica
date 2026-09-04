using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Qué permisos trae cada rol. Es la única fuente de verdad de la matriz de
    /// accesos, y vive en Domain porque es una regla de negocio (segregación de
    /// funciones), no un detalle de la capa web.
    ///
    /// Dos criterios que explican el reparto:
    ///
    /// 1. "Ver" viene con la acción: quien registra gastos necesita la pantalla de
    ///    gastos para trabajar. El historial además lo ven Custodio y Gerente por
    ///    transparencia, para que puedan comprobar algo sin pedírselo al Auditor.
    ///
    /// 2. El Administrador NO tiene permisos operativos. Configura fondos, límites,
    ///    categorías y usuarios, pero no registra gastos ni aprueba reposiciones:
    ///    quien fija el límite no debe poder gastarse el fondo. Es lo que exige una
    ///    auditoría de caja chica y por eso el Auditor tampoco tiene ninguna acción,
    ///    solo lectura.
    ///
    /// 3. Anular un gasto son dos permisos y no uno: el Custodio solo puede
    ///    <see cref="Permisos.SolicitarAnulacionGasto"/> (deja el gasto pendiente, sin
    ///    devolver el dinero) y el Gerente tiene <see cref="Permisos.AnularGasto"/>,
    ///    que es el que efectivamente devuelve el efectivo al fondo. Con un solo
    ///    permiso, quien registra el gasto podría deshacerlo sin que nadie lo revise.
    ///
    /// 4. <see cref="Permisos.GestionarPerfilPropio"/> lo tienen TODOS los roles: no es
    ///    una acción de negocio sujeta a segregación de funciones, sino algo que
    ///    cualquiera hace sobre su propia cuenta. Se reparte por la matriz igual que el
    ///    resto para que no haya excepciones que buscar sueltas por los endpoints. Una
    ///    cuenta sin rol (Pendiente de aprobación) sigue sin tenerlo, que es lo
    ///    correcto: todavía no debería poder tocar nada.
    /// </summary>
    public static class PermisosPorRol
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Mapa =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [RolesApp.Custodio] = new HashSet<string>
                {
                    Permisos.VerGastos,
                    Permisos.RegistrarGasto,
                    Permisos.SolicitarAnulacionGasto,
                    Permisos.VerArqueos,
                    Permisos.EjecutarArqueo,
                    Permisos.VerReposiciones,
                    Permisos.SolicitarReposicion,
                    Permisos.DescargarExpediente,
                    Permisos.ConsultarHistorial,
                    Permisos.GestionarPerfilPropio
                },

                [RolesApp.Administrador] = new HashSet<string>
                {
                    Permisos.ConfigurarFondos,
                    Permisos.ConfigurarCategorias,
                    Permisos.AdministrarUsuarios,
                    Permisos.GestionarPerfilPropio
                },

                [RolesApp.Gerente] = new HashSet<string>
                {
                    Permisos.VerGastos,
                    Permisos.RevisarGastos,
                    Permisos.AnularGasto,
                    Permisos.VerArqueos,
                    Permisos.VerReposiciones,
                    Permisos.AprobarReposicion,
                    Permisos.DescargarExpediente,
                    Permisos.ConsultarHistorial,
                    Permisos.GestionarPerfilPropio
                },

                [RolesApp.Finanzas] = new HashSet<string>
                {
                    Permisos.VerReposiciones,
                    Permisos.PagarReposicion,
                    Permisos.DescargarExpediente,
                    Permisos.GestionarPerfilPropio
                },

                [RolesApp.Auditor] = new HashSet<string>
                {
                    Permisos.VerGastos,
                    Permisos.VerArqueos,
                    Permisos.VerReposiciones,
                    Permisos.DescargarExpediente,
                    Permisos.ConsultarHistorial,
                    Permisos.GestionarPerfilPropio
                }
            };

        /// <summary>
        /// Un rol desconocido (o vacío) no otorga nada. Es deliberado: un usuario sin
        /// rol asignado todavía no fue aprobado por un Administrador, y lo correcto
        /// mientras tanto es que no pueda hacer nada.
        /// </summary>
        public static bool RolTienePermiso(string? rol, string permiso) =>
            !string.IsNullOrWhiteSpace(rol)
            && Mapa.TryGetValue(rol, out var permisos)
            && permisos.Contains(permiso);

        public static IReadOnlySet<string> Obtener(string? rol) =>
            !string.IsNullOrWhiteSpace(rol) && Mapa.TryGetValue(rol, out var permisos)
                ? permisos
                : new HashSet<string>();
    }
}
