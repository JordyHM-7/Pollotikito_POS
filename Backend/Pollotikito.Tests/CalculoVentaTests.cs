using Pollotikito.Application;
using Pollotikito.Domain;

namespace Pollotikito.Tests;

public sealed class CalculoVentaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static List<LineaVenta> Calcular(Producto p, int cantidad = 1, DescuentoSolicitud? manual = null, DescuentoSolicitud? global = null) =>
        CalculoVenta.Lineas([new(p.Id, cantidad, null, manual)], new Dictionary<Guid,Producto> { [p.Id] = p }, global, Ahora);

    [Theory, Trait("Requerimiento", "RF-CAT-03")]
    [InlineData("1130", "13", "1000", "130")]
    [InlineData("0.05", "13", "0.04", "0.01")]
    [InlineData("0.03", "20", "0.03", "0.00")]
    [InlineData("2500", "0", "2500", "0")]
    public void Iva_incluido_redondea_alejandose_de_cero(string precio, string iva, string baseEsperada, string impuesto)
    {
        static decimal D(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        var l = Assert.Single(Calcular(new Producto { Precio = D(precio), TarifaIva = D(iva) }));
        Assert.Equal(D(baseEsperada), l.Base); Assert.Equal(D(impuesto), l.Impuesto); Assert.Equal(l.Total, l.Base + l.Impuesto);
    }
    [Fact, Trait("Requerimiento", "RF-CAT-04")]
    public void Orden_automatico_por_unidad_manual_de_linea_y_global()
    {
        var p = new Producto { Precio = 1130, TarifaIva = 13, TipoDescuento = TipoDescuento.Monto, ValorDescuento = 100 };
        var l = Assert.Single(Calcular(p, 2, new(TipoDescuento.Porcentaje, 10, "Promoción"), new(TipoDescuento.Monto, 54, "Cliente")));
        Assert.Equal(200m, l.DescuentoAutomatico); Assert.Equal(206m, l.DescuentoManual); Assert.Equal(54m, l.DescuentoGlobal);
        Assert.Equal(1800m, l.Total); Assert.Equal(1592.92m, l.Base); Assert.Equal(207.08m, l.Impuesto);
    }
    [Theory, Trait("Requerimiento", "RF-CAT-04")]
    [InlineData(-2, -1, false)] [InlineData(1, 2, false)] [InlineData(-1, 1, true)] [InlineData(0, 0, true)]
    public void Descuento_automatico_respeta_vigencia_inclusiva(int desde, int hasta, bool aplica)
    {
        var p = new Producto { Precio = 100, TipoDescuento = TipoDescuento.Porcentaje, ValorDescuento = 20, DescuentoDesde = Ahora.AddDays(desde), DescuentoHasta = Ahora.AddDays(hasta) };
        Assert.Equal(aplica ? 80m : 100m, Assert.Single(Calcular(p)).Total);
    }
    [Fact, Trait("Requerimiento", "RF-CAT-04")]
    public void Porcentaje_automatico_redondea_por_unidad_y_monto_se_limita_al_precio()
    {
        var p = new Producto { Precio = .05m, TipoDescuento = TipoDescuento.Porcentaje, ValorDescuento = 10 };
        Assert.Equal(.03m, Assert.Single(Calcular(p, 3)).DescuentoAutomatico);
        p.TipoDescuento = TipoDescuento.Monto; p.ValorDescuento = 100;
        Assert.Equal(0, Assert.Single(Calcular(p, 3)).Total);
    }
    [Fact, Trait("Requerimiento", "RF-CAT-04")]
    public void Prorrateo_distribuye_centavos_de_forma_estable_sin_perder_dinero()
    {
        var p = new Producto { Precio = .01m };
        var d = new Dictionary<Guid,Producto> { [p.Id] = p };
        var lineas = CalculoVenta.Lineas([new(p.Id,1), new(p.Id,1), new(p.Id,1)], d, new(TipoDescuento.Monto,.02m,"Redondeo"), Ahora);
        Assert.Equal(.02m, lineas.Sum(l => l.DescuentoGlobal)); Assert.Equal(.01m, lineas.Sum(l => l.Total));
        Assert.Equal(new[] { 0m, 0m, .01m }, lineas.Select(l => l.Total));
        Assert.All(lineas, l => Assert.True(l.Total >= 0));
    }
    [Fact, Trait("Requerimiento", "RF-CAT-04")]
    public void Manual_requiere_motivo_y_no_permite_negativos()
    {
        Assert.Throws<ErrorAplicacion>(() => Calcular(new Producto { Precio = 100 }, manual:new(TipoDescuento.Monto, 1)));
        Assert.Throws<ErrorAplicacion>(() => Calcular(new Producto { Precio = 100 }, manual:new(TipoDescuento.Monto, 101, "Error")));
    }
    [Fact, Trait("Requerimiento", "RF-VEN-03")]
    public void Pagos_mixtos_solo_efectivo_genera_vuelto()
    {
        var pagos = CalculoVenta.Pagos([new(MetodoPago.Efectivo, 300, 1000), new(MetodoPago.Tarjeta,500,null,"A1"), new(MetodoPago.Sinpe,200,null,"S1")],1000);
        Assert.Equal(700m, pagos.Sum(p => p.Vuelto)); Assert.Equal(300m, pagos[0].Recibido - pagos[0].Vuelto);
        Assert.Throws<ErrorAplicacion>(() => CalculoVenta.Pagos([new(MetodoPago.Tarjeta,1000,2000,"A")],1000));
        Assert.Throws<ErrorAplicacion>(() => CalculoVenta.Pagos([new(MetodoPago.Sinpe,1000)],1000));
        Assert.Throws<ErrorAplicacion>(() => CalculoVenta.Pagos([new(MetodoPago.Efectivo,999,1000)],1000));
    }
}
