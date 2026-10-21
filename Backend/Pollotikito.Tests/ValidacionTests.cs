using Pollotikito.Application;

namespace Pollotikito.Tests;
public sealed class ValidacionTests
{
    [Theory, Trait("Requerimiento", "RF-SEG-02")]
    [InlineData("123")][InlineData("1234567")][InlineData("12ab")][InlineData("１２３４")]
    public void Pin_invalido_es_rechazado(string pin) => Assert.Throws<ErrorAplicacion>(() => Validacion.Pin(pin));
    [Theory, Trait("Requerimiento", "RF-SEG-02")]
    [InlineData("1234")][InlineData("123456")]
    public void Pin_valido_es_aceptado(string pin) => Validacion.Pin(pin);
    [Fact, Trait("Requerimiento", "RF-SIS-02")]
    public void Logo_invalido_o_excesivo_es_rechazado()
    {
        Assert.Throws<ErrorAplicacion>(() => Validacion.Logo("<svg/>"u8.ToArray()));
        Assert.Throws<ErrorAplicacion>(() => Validacion.Logo(new byte[1048577]));
    }
}
