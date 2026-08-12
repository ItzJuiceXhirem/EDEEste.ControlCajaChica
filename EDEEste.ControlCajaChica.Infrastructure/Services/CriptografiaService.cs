using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Interfaces;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    public class CriptografiaService : ICriptografiaService
    {
        private static readonly byte[] SecretKey = Encoding.UTF8.GetBytes("Super_Duper_Policia_LlaveSecreta_Edeeste2026");

        public string CalcularHMAC(string datos)
        {
            /* HMACSHA256 genera un hash único basado en los datos y la llave secreta.
 Si cambia un solo carácter de los datos o si no se tiene la llave, el hash cambia por completo.*/
            using var hmac = new HMACSHA256(SecretKey);
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(datos));
            return Convert.ToHexString(hashBytes);
        }

        public bool ValidarIntegridad(ITamperProofEntity entidad)
        {
            var hashCalculado = CalcularHMAC(entidad.ObtenerCadenaParaHash());
            return hashCalculado == entidad.HashFirma;
        }
    }
}
