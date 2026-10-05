using EDEEste.ControlCajaChica.Application.Features.Auditoria;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Auditoria
{
    public class DiferenciaDeValoresTests
    {
        [Fact]
        public void Creacion_ListaTodosLosCamposConValor()
        {
            var resultado = DiferenciaDeValores.Calcular(null, """{"Proveedor":"ABC","Concepto":null,"MontoTotal":2500.0000}""");

            Assert.True(resultado.Legible);
            Assert.Collection(resultado.Cambios,
                cambio => Assert.Equal(new CambioDeCampo("Proveedor", null, "ABC"), cambio),
                cambio => Assert.Equal(new CambioDeCampo("MontoTotal", null, "2500.0000"), cambio));
        }

        [Fact]
        public void Modificacion_ListaSoloLosCamposQueCambiaron()
        {
            var resultado = DiferenciaDeValores.Calcular(
                """{"Estado":2,"MontoTotal":100,"Proveedor":"ABC"}""",
                """{"Estado":3,"MontoTotal":100,"Proveedor":"ABC"}""");

            var cambio = Assert.Single(resultado.Cambios);
            Assert.Equal(new CambioDeCampo("Estado", "2", "3"), cambio);
        }

        [Fact]
        public void BorradoLogico_ListaElCambioDeIsDeleted()
        {
            var resultado = DiferenciaDeValores.Calcular("""{"IsDeleted":false}""", """{"IsDeleted":true}""");

            var cambio = Assert.Single(resultado.Cambios);
            Assert.Equal(new CambioDeCampo("IsDeleted", "false", "true"), cambio);
        }

        [Fact]
        public void CampoQueSoloEstabaAntes_SeListaComoQuitado()
        {
            var resultado = DiferenciaDeValores.Calcular("""{"Viejo":"x"}""", "{}");

            var cambio = Assert.Single(resultado.Cambios);
            Assert.Equal(new CambioDeCampo("Viejo", "x", null), cambio);
        }

        [Theory]
        [InlineData("{no es json")]
        [InlineData("[1,2,3]")]
        public void JsonIlegible_NoLanzaYSeMarcaComoIlegible(string json)
        {
            var resultado = DiferenciaDeValores.Calcular(json, """{"A":1}""");

            Assert.False(resultado.Legible);
            Assert.Empty(resultado.Cambios);
        }
    }
}
