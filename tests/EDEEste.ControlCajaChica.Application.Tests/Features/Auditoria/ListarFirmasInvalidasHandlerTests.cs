using System;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Application.Features.Auditoria;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Auditoria
{
    public class ListarFirmasInvalidasHandlerTests
    {
        [Fact]
        public async Task ConPermiso_DevuelveLoQueEncontroElVerificador()
        {
            var verificador = new FakeVerificadorIntegridadDatos();
            verificador.FirmasInvalidas.Add(new RegistroConFirmaInvalidaDto { NombreTabla = "Gastos", Id = Guid.NewGuid() });

            var resultado = await new ListarFirmasInvalidasHandler(verificador, new FakeAutorizacionService()).EjecutarAsync();

            Assert.True(resultado.Exitoso);
            Assert.Single(resultado.Valor!);
        }

        [Fact]
        public async Task SinPermiso_FallaSinConsultarNada()
        {
            var verificador = new FakeVerificadorIntegridadDatos();
            verificador.FirmasInvalidas.Add(new RegistroConFirmaInvalidaDto { NombreTabla = "Gastos", Id = Guid.NewGuid() });

            var resultado = await new ListarFirmasInvalidasHandler(verificador, new FakeAutorizacionService(false)).EjecutarAsync();

            Assert.False(resultado.Exitoso);
            Assert.Null(resultado.Valor);
        }
    }
}
