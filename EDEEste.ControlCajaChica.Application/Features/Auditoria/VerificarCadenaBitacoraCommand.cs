namespace EDEEste.ControlCajaChica.Application.Features.Auditoria
{
    // Recorre la cadena de la bitácora completa y comprueba cada firma y cada enlace
    public sealed class VerificarCadenaBitacoraCommand
    {
      /* La huella que el Auditor anotó en una revisión anterior (el HashFirma de la
         que entonces era la última fila). Opcional: si se indica, se comprueba que
         siga dentro de la cadena, que es lo único que delata un borrado de las
         últimas filas. */
        public string? HuellaAnterior { get; set; }
    }
}
