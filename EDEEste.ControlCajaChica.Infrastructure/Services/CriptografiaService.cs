using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    public sealed class CriptografiaService : ICriptografiaService
    {
        private readonly byte[] _claveSecreta;

        public CriptografiaService(IOptions<OpcionesCriptografia> opciones)
        {
            _claveSecreta = Convert.FromBase64String(opciones.Value.ClaveHmac);
        }

        /// <summary>
        /// HMACSHA256 genera un hash unico a partir de los datos y la llave secreta.
        /// Si cambia un solo caracter de los datos, o si no se tiene la llave, el
        /// hash resultante es completamente distinto.
        /// </summary>
        public string CalcularHMAC(string datos)
        {
            using var hmac = new HMACSHA256(_claveSecreta);
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(datos));
            return Convert.ToHexString(hashBytes);
        }

        public bool ValidarFirma(string datos, string firmaGuardada)
        {
            if (string.IsNullOrEmpty(firmaGuardada))
            {
                // Fila sin firmar: no se puede afirmar que sea integra.
                return false;
            }

            var firmaEsperada = CalcularHMAC(datos);

            // Comparacion en tiempo constante: un == normal corta en el primer byte
            // distinto y filtra, por diferencias de tiempo, cuanto prefijo acerto
            // quien este probando firmas.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(firmaEsperada),
                Encoding.UTF8.GetBytes(firmaGuardada));
        }

        public bool ValidarIntegridad(ITamperProofEntity entidad)
        {
            ArgumentNullException.ThrowIfNull(entidad);

            return ValidarFirma(entidad.ObtenerCadenaParaHash(), entidad.HashFirma);
        }
    }
}
