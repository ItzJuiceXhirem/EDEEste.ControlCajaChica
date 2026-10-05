using System;
using System.Security.Cryptography;
using System.Text;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Interfaces;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    /// <summary>
    /// HMACSHA256 de verdad, con una clave fija de prueba: determinista, y con las
    /// mismas propiedades que importan en la verificación de la cadena (un byte
    /// distinto da una firma completamente distinta). Un hash de mentira (por ejemplo
    /// devolver los mismos datos) haría pasar pruebas que con el real fallarían.
    /// </summary>
    public sealed class FakeCriptografiaService : ICriptografiaService
    {
        private static readonly byte[] ClaveDePrueba = "clave-hmac-solo-para-pruebas-unitarias"u8.ToArray();

        public string CalcularHMAC(string datos)
        {
            using var hmac = new HMACSHA256(ClaveDePrueba);
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(datos)));
        }

        public bool ValidarFirma(string datos, string firmaGuardada) =>
            !string.IsNullOrEmpty(firmaGuardada)
            && string.Equals(CalcularHMAC(datos), firmaGuardada, StringComparison.Ordinal);

        public bool ValidarIntegridad(ITamperProofEntity entidad) =>
            ValidarFirma(entidad.ObtenerCadenaParaHash(), entidad.HashFirma);
    }
}
