using Pollotikito.Domain;

namespace Pollotikito.Application;

public static class CalculoVenta
{
    public static readonly int[] Denominaciones = [5, 10, 25, 50, 100, 500, 1000, 2000, 5000, 10000, 20000];
    public const decimal MaximoMonto = 999_999_999_999m;
    public static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
    public static void Dinero(decimal monto, string campo, bool positivo = false) =>
        Validacion.Exigir(monto >= 0 && monto <= MaximoMonto && Redondear(monto) == monto && (!positivo || monto > 0), $"{campo}: monto inválido.");

    public static void ValidarDescuento(TipoDescuento tipo, decimal valor)
    {
        Validacion.Exigir(Enum.IsDefined(tipo), "Tipo de descuento inválido.");
        Dinero(valor, "Descuento");
        Validacion.Exigir(tipo != TipoDescuento.Ninguno || valor == 0, "Un descuento sin tipo debe valer cero.");
        Validacion.Exigir(tipo != TipoDescuento.Porcentaje || valor <= 100, "El porcentaje no puede superar 100.");
    }
    public static decimal Manual(decimal disponible, DescuentoSolicitud? descuento)
    {
        if (descuento == null) return 0;
        ValidarDescuento(descuento.Tipo, descuento.Valor);
        if (descuento.Tipo != TipoDescuento.Ninguno && descuento.Valor > 0)
            Validacion.Texto(descuento.Motivo, "Motivo de descuento", 500, true);
        var monto = descuento.Tipo switch { TipoDescuento.Porcentaje => Redondear(disponible * descuento.Valor / 100m), TipoDescuento.Monto => descuento.Valor, _ => 0 };
        Validacion.Exigir(monto <= disponible, "El descuento no puede dejar una línea o venta por debajo de cero.");
        return monto;
    }

    public static List<LineaVenta> Lineas(List<LineaSolicitud> solicitudes, IReadOnlyDictionary<Guid, Producto> productos,
        DescuentoSolicitud? global, DateTimeOffset ahora, DefinicionMenu? menu = null)
    {
        Validacion.Exigir(solicitudes is { Count: > 0 and <= 200 }, "La venta debe tener entre 1 y 200 líneas.");
        var lineas = new List<LineaVenta>();
        foreach (var s in solicitudes)
        {
            Validacion.Exigir(s != null && productos.ContainsKey(s.ProductoId), "Producto no disponible.");
            var p = productos[s!.ProductoId];
            Validacion.Exigir(s.Cantidad is > 0 and <= 9999, "Cantidad inválida.");
            Validacion.Exigir(p.Disponible,"Producto no disponible.");
            var elecciones=ReglasMenu.Elegir(s,p,productos,menu);
            var precio=p.Precio+elecciones.Adicional;Dinero(precio,"Precio unitario completo");
            var bruto = precio * s.Cantidad; Dinero(bruto, "Subtotal");
            var vigente = (p.DescuentoDesde == null || ahora >= p.DescuentoDesde) && (p.DescuentoHasta == null || ahora <= p.DescuentoHasta);
            var porUnidad = !vigente ? 0 : p.TipoDescuento switch {
                TipoDescuento.Porcentaje => Redondear(precio * p.ValorDescuento / 100m),
                TipoDescuento.Monto => Math.Min(precio, p.ValorDescuento), _ => 0 };
            var automatico = porUnidad * s.Cantidad;
            var manual = Manual(bruto - automatico, s.Descuento);
            lineas.Add(new LineaVenta { ProductoId = p.Id, Orden = lineas.Count, Nombre = p.Nombre, Cantidad = s.Cantidad,
                Nota = Validacion.Texto(s.Nota, "Nota", 500), PrecioUnitario = precio, TarifaIva = p.TarifaIva,
                Modificadores=elecciones.Modificadores,Componentes=elecciones.Componentes,
                DescuentoAutomatico = automatico, DescuentoManual = manual, TipoDescuentoManual = s.Descuento?.Tipo ?? TipoDescuento.Ninguno,
                ValorDescuentoManual = s.Descuento?.Valor ?? 0, MotivoDescuento = s.Descuento?.Motivo?.Trim(), Total = bruto - automatico - manual });
        }
        var subtotal = lineas.Sum(x => x.Total); Dinero(subtotal, "Total");
        var descuentoGlobal = Manual(subtotal, global);
        if (descuentoGlobal > 0)
        {
            // Cuotas inferiores en centavos y mayores restos: suma exacta, orden estable y sin negativos.
            var cuotas = lineas.Select((l, i) => new { Indice = i, Exacta = descuentoGlobal * l.Total / subtotal * 100m }).ToList();
            foreach (var c in cuotas) lineas[c.Indice].DescuentoGlobal = decimal.Floor(c.Exacta) / 100m;
            var faltan = (int)Redondear((descuentoGlobal - lineas.Sum(x => x.DescuentoGlobal)) * 100m);
            foreach (var c in cuotas.OrderByDescending(x => x.Exacta - decimal.Floor(x.Exacta)).ThenBy(x => x.Indice).Take(faltan))
                lineas[c.Indice].DescuentoGlobal += .01m;
        }
        foreach (var l in lineas)
        {
            foreach(var m in l.Modificadores)m.LineaVentaId=l.Id;
            foreach(var c in l.Componentes)c.LineaVentaId=l.Id;
            l.Total -= l.DescuentoGlobal;
            l.Base = Redondear(l.Total / (1m + l.TarifaIva / 100m)); l.Impuesto = l.Total - l.Base;
        }
        return lineas;
    }

    public static List<PagoVenta> Pagos(List<PagoSolicitud> solicitudes, decimal total)
    {
        Validacion.Exigir(solicitudes is { Count: > 0 and <= 3 }, "Indicá entre 1 y 3 métodos de pago.");
        Validacion.Exigir(solicitudes.All(p => p != null && Enum.IsDefined(p.Metodo)), "Método de pago inválido.");
        Validacion.Exigir(solicitudes.Select(p => p.Metodo).Distinct().Count() == solicitudes.Count, "No se puede repetir un método de pago.");
        var pagos = new List<PagoVenta>();
        foreach (var p in solicitudes)
        {
            Dinero(p.Monto, "Pago", total > 0);
            if (p.Metodo == MetodoPago.Efectivo)
            {
                Validacion.Exigir(p.Recibido != null, "Indicá el efectivo recibido.");
                Dinero(p.Recibido!.Value, "Efectivo recibido");
                Validacion.Exigir(p.Recibido >= p.Monto, "Efectivo recibido insuficiente.");
            }
            else
            {
                Validacion.Texto(p.Referencia, "Autorización o comprobante", 100, true);
                Validacion.Exigir(p.Recibido == null, "Solo el efectivo puede tener recibido y vuelto.");
            }
            pagos.Add(new PagoVenta { Metodo = p.Metodo, Monto = p.Monto, Recibido = p.Recibido,
                Vuelto = p.Metodo == MetodoPago.Efectivo ? p.Recibido!.Value - p.Monto : 0, Referencia = p.Referencia?.Trim() });
        }
        Validacion.Exigir(pagos.Sum(x => x.Monto) == total, "La suma de los pagos aplicados debe ser igual al total de la venta.");
        return pagos;
    }
}
