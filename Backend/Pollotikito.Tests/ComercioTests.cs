using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pollotikito.Application;
using Pollotikito.Domain;
using Pollotikito.Infrastructure;

namespace Pollotikito.Tests;

public sealed class ComercioTests(PostgresFixture f) : IClassFixture<PostgresFixture>
{
    private async Task<T> Leer<T>(HttpResponseMessage r)
    { Assert.True(r.IsSuccessStatusCode, $"HTTP {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}"); return (await r.Content.ReadFromJsonAsync<T>(PostgresFixture.Json))!; }
    private sealed class Turno(HttpClient admin, HttpClient cajero, SesionCaja sesion, Categoria categoria, Producto producto) : IAsyncDisposable
    {
        public HttpClient Admin { get; } = admin; public HttpClient Cajero { get; } = cajero;
        public SesionCaja Sesion { get; } = sesion; public Categoria Categoria { get; } = categoria; public Producto Producto { get; } = producto;
        public async ValueTask DisposeAsync()
        { await Admin.PostAsJsonAsync($"/api/caja/{Sesion.Id}/forzar-cierre", new CerrarCaja(null, "Fin de prueba")); Admin.Dispose(); Cajero.Dispose(); }
        public CobrarVenta Solicitud(List<PagoSolicitud>? pagos = null, DescuentoSolicitud? descuento = null) => new(Guid.CreateVersion7(), Sesion.Id, TipoOrden.ComerAqui, [new(Producto.Id,1)], pagos ?? [new(MetodoPago.Efectivo,1130,2000)], descuento);
    }
    private async Task<Turno> Abrir()
    {
        var a = await f.Admin(); var cat = await Leer<Categoria>(await a.PostAsJsonAsync("/api/categorias", new DatosCategoria("Categoría " + Guid.NewGuid(), 1)));
        var p = await Leer<Producto>(await a.PostAsJsonAsync("/api/productos", new DatosProducto(cat.Id,"Pollo",1130,13,1)));
        var u = await f.CrearCajero(a); var c = await f.Cajero(u.Id);
        var s = await Leer<SesionCaja>(await c.PostAsJsonAsync("/api/caja/abrir", new AbrirCaja(10000)));
        return new Turno(a,c,s,cat,p);
    }
    [PostgresFact, Trait("Requerimiento","RF-CAT-01"), Trait("Requerimiento","RF-CAT-02")]
    public async Task Catalogo_admin_403_para_cajero_y_categoria_inactiva_excluye_producto_activo()
    {
        await using var t = await Abrir();
        foreach (var ruta in new[] {"/api/categorias","/api/productos"}) Assert.Equal(HttpStatusCode.Forbidden,(await t.Cajero.GetAsync(ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await t.Cajero.PostAsJsonAsync("/api/categorias",new DatosCategoria("No",0))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await t.Cajero.PutAsJsonAsync($"/api/productos/{t.Producto.Id}", new DatosProducto(t.Categoria.Id,"No",1,13,0))).StatusCode);
        (await t.Admin.PostAsync($"/api/categorias/{t.Categoria.Id}/desactivar",null)).EnsureSuccessStatusCode();
        var catalogo = await Leer<CatalogoDto>(await t.Cajero.GetAsync("/api/caja/catalogo"));
        Assert.DoesNotContain(catalogo.Productos,p => p.Id == t.Producto.Id);
        Assert.Equal(HttpStatusCode.BadRequest,(await t.Cajero.PostAsJsonAsync("/api/ventas",t.Solicitud())).StatusCode);
    }
    [PostgresFact, Trait("Requerimiento","RF-VEN-03")]
    public async Task Servidor_ignora_precios_cliente_rechaza_pagos_y_guarda_instantanea()
    {
        await using var t = await Abrir();
        var id = Guid.CreateVersion7();
        var r = await t.Cajero.PostAsJsonAsync("/api/ventas", new { id, sesionCajaId=t.Sesion.Id, tipoOrden="Express", total=1,
            lineas=new[]{new {productoId=t.Producto.Id,cantidad=1,precioUnitario=1}},pagos=new[]{new {metodo="Efectivo",monto=1,recibido=1}} });
        Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
        var v = await Leer<VentaDto>(await t.Cajero.PostAsJsonAsync("/api/ventas",t.Solicitud()));
        Assert.Equal(1130m,v.Total); Assert.Equal(1000m,v.Base); Assert.Equal(130m,v.Impuesto);
        (await t.Admin.PutAsJsonAsync($"/api/productos/{t.Producto.Id}",new DatosProducto(t.Categoria.Id,"Pollo nuevo",2260,13,1))).EnsureSuccessStatusCode();
        var pasada = await Leer<VentaDto>(await t.Cajero.GetAsync($"/api/ventas/{v.Id}"));
        Assert.Equal(1130m,pasada.Total); Assert.Equal("Pollo",Assert.Single(pasada.Lineas).Nombre);
    }
    [PostgresFact, Trait("Requerimiento","RF-VEN-03")]
    public async Task Idempotencia_identica_retorna_original_y_contenido_distinto_conflicto()
    {
        await using var t = await Abrir(); var d=t.Solicitud();
        var respuestas=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>t.Cajero.PostAsJsonAsync("/api/ventas",d)));
        var ventas=new List<VentaDto>(); foreach(var r in respuestas) ventas.Add(await Leer<VentaDto>(r));
        Assert.Single(ventas.Select(x=>x.Numero).Distinct());
        Assert.Equal(HttpStatusCode.Conflict,(await t.Cajero.PostAsJsonAsync("/api/ventas", d with {Lineas=[new(t.Producto.Id,2)]})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await t.Cajero.PostAsJsonAsync("/api/ventas", d with {Pagos=[new(MetodoPago.Efectivo,1130,5000)]})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await t.Cajero.PostAsJsonAsync("/api/ventas", d with {SesionCajaId=Guid.CreateVersion7()})).StatusCode);
        using var scope=f.Api.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();
        Assert.Equal(1,await db.Set<Venta>().CountAsync(v=>v.Id==d.Id));
    }
    [PostgresFact, Trait("Requerimiento","RF-VEN-03")]
    public async Task Cobros_paralelos_no_repiten_consecutivo_y_no_usan_bloqueo_global()
    {
        await using var t = await Abrir();
        await using var conn=new NpgsqlConnection(f.Conexion); await conn.OpenAsync(); await using var tx=await conn.BeginTransactionAsync();
        await using(var cmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(71002101)",conn,tx)) await cmd.ExecuteNonQueryAsync();
        var respuestas=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>t.Cajero.PostAsJsonAsync("/api/ventas",t.Solicitud()))).WaitAsync(TimeSpan.FromSeconds(15));
        var ventas=new List<VentaDto>(); foreach(var r in respuestas) ventas.Add(await Leer<VentaDto>(r));
        Assert.Equal(12,ventas.Select(v=>v.Numero).Distinct().Count());
        await tx.RollbackAsync();
    }
    [PostgresFact, Trait("Requerimiento","RF-CAJ-01")]
    public async Task Sin_caja_propia_no_se_vende_ni_se_abre_segunda_sesion()
    {
        await using var t = await Abrir(); var u=await f.CrearCajero(t.Admin); using var otro=await f.Cajero(u.Id);
        Assert.Equal(HttpStatusCode.Forbidden,(await otro.PostAsJsonAsync("/api/ventas",t.Solicitud())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await otro.PostAsJsonAsync("/api/caja/abrir",new AbrirCaja(0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await otro.PostAsJsonAsync("/api/ventas",t.Solicitud() with {SesionCajaId=Guid.CreateVersion7()})).StatusCode);
    }
    private async Task<string> Token(Turno t,string accion)
    { var r=await Leer<AutorizacionDto>(await t.Cajero.PostAsJsonAsync("/api/autorizaciones/administrador",new PedirAutorizacion(f.AdminId,"8642",accion))); return r.Token; }
    private static async Task<HttpResponseMessage> Retirar(Turno t,string? token)
    {
        var r=new HttpRequestMessage(HttpMethod.Post,$"/api/caja/{t.Sesion.Id}/retiros"){Content=JsonContent.Create(new DatosMovimiento(200,"Compra"))};
        if(token!=null)r.Headers.Add("X-Autorizacion-Administrador",token);return await t.Cajero.SendAsync(r);
    }
    [PostgresFact, Trait("Requerimiento","RF-CAJ-02")]
    public async Task Deposito_sin_token_y_retiro_solo_con_accion_correcta_de_un_uso()
    {
        await using var t=await Abrir();
        (await t.Cajero.PostAsJsonAsync($"/api/caja/{t.Sesion.Id}/depositos",new DatosMovimiento(500,"Cambio"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden,(await Retirar(t,null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Retirar(t,await Token(t,"prueba"))).StatusCode);
        var token=await Token(t,"retiro_efectivo");(await Retirar(t,token)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden,(await Retirar(t,token)).StatusCode);
    }
    [PostgresFact, Trait("Requerimiento","RF-CAJ-03"), Trait("Requerimiento","RF-CAJ-04"), Trait("Requerimiento","RF-VEN-09")]
    public async Task Esperado_con_mixto_vuelto_movimientos_anulacion_y_cierre_inmutable()
    {
        await using var t=await Abrir();
        await Leer<VentaDto>(await t.Cajero.PostAsJsonAsync("/api/ventas",t.Solicitud()));
        await Leer<VentaDto>(await t.Cajero.PostAsJsonAsync("/api/ventas",t.Solicitud([new(MetodoPago.Efectivo,130,1000),new(MetodoPago.Tarjeta,1000,null,"A1")])));
        var anulada=await Leer<VentaDto>(await t.Cajero.PostAsJsonAsync("/api/ventas",t.Solicitud([new(MetodoPago.Sinpe,1130,null,"S1")])));
        var anulacion=await t.Cajero.PostAsJsonAsync($"/api/ventas/{anulada.Id}/anular",new AnularVenta("Error de pedido"));anulacion.EnsureSuccessStatusCode();
        Assert.Contains("Recordá reversar",await anulacion.Content.ReadAsStringAsync());
        (await t.Cajero.PostAsJsonAsync($"/api/caja/{t.Sesion.Id}/depositos",new DatosMovimiento(500,"Cambio"))).EnsureSuccessStatusCode();
        (await Retirar(t,await Token(t,"retiro_efectivo"))).EnsureSuccessStatusCode();
        var reporte=await Leer<ReporteCaja>(await t.Cajero.GetAsync($"/api/caja/{t.Sesion.Id}/reporte"));
        Assert.Equal(11560m,reporte.Esperado);Assert.Equal(2,reporte.CantidadVentas);Assert.Equal(1,reporte.CantidadAnulaciones);
        Assert.Equal(1130m,reporte.AnulacionesPorMetodo.Single(x=>x.Metodo==MetodoPago.Sinpe).Monto);
        var cierre=await Leer<ReporteCaja>(await t.Cajero.PostAsJsonAsync($"/api/caja/{t.Sesion.Id}/cerrar",new CerrarCaja([new(10000,1),new(1000,1),new(500,1),new(50,1),new(10,1)])));
        Assert.Equal(11560m,cierre.Contado);Assert.Equal(0m,cierre.Diferencia);
        Assert.Equal(HttpStatusCode.Conflict,(await t.Cajero.PostAsJsonAsync($"/api/ventas/{anulada.Id}/anular",new AnularVenta("Otra"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await t.Cajero.PostAsJsonAsync("/api/ventas",t.Solicitud())).StatusCode);
        using var scope=f.Api.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();
        await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sesiones_caja SET monto_inicial=0 WHERE id={t.Sesion.Id}"));
    }
    [PostgresFact, Trait("Requerimiento","RF-CAJ-03")]
    public async Task Cierre_forzado_sin_conteo_es_explicito_y_no_inventa_cero()
    {
        await using var t=await Abrir();
        var r=await Leer<ReporteCaja>(await t.Admin.PostAsJsonAsync($"/api/caja/{t.Sesion.Id}/forzar-cierre",new CerrarCaja(null,"Cajero ausente")));
        Assert.True(r.Sesion.CerradaSinConteo);Assert.Null(r.Contado);Assert.Null(r.Diferencia);Assert.Equal(10000m,r.Esperado);
        var consulta=await Leer<ReporteCaja>(await t.Admin.GetAsync($"/api/caja/{t.Sesion.Id}/reporte"));Assert.True(consulta.Sesion.CerradaSinConteo);
    }
    [PostgresFact, Trait("Requerimiento","RF-SEG-04"), Trait("Requerimiento","RF-CAT-04")]
    public async Task Precio_descuento_manual_y_anulacion_dejan_auditoria()
    {
        await using var t=await Abrir();
        (await t.Admin.PutAsJsonAsync($"/api/productos/{t.Producto.Id}",new DatosProducto(t.Categoria.Id,"Pollo",2260,13,1))).EnsureSuccessStatusCode();
        var v=await Leer<VentaDto>(await t.Cajero.PostAsJsonAsync("/api/ventas",t.Solicitud([new(MetodoPago.Efectivo,2160,3000)],new(TipoDescuento.Monto,100,"Cortesía"))));
        (await t.Cajero.PostAsJsonAsync($"/api/ventas/{v.Id}/anular",new AnularVenta("Error"))).EnsureSuccessStatusCode();
        using var scope=f.Api.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();
        var b=await db.Bitacoras.SingleAsync(b=>b.EntidadId==t.Producto.Id&&b.Accion=="Editar");
        Assert.Contains("1130",b.ValorAnterior!);Assert.Contains("2260",b.ValorNuevo!);
        Assert.True(await db.Bitacoras.AnyAsync(b=>b.EntidadId==v.Id&&b.Accion=="DescuentoManual"));
        Assert.True(await db.Bitacoras.AnyAsync(b=>b.EntidadId==v.Id&&b.Accion=="AnularVenta"));
    }

    [PostgresFact, Trait("Requerimiento","RF-CAJ-01")]
    public async Task Aperturas_simultaneas_dejan_exactamente_una_caja_abierta()
    {
        using var a=await f.Admin(); var u1=await f.CrearCajero(a);var u2=await f.CrearCajero(a);
        using var c1=await f.Cajero(u1.Id);using var c2=await f.Cajero(u2.Id);
        var resultados=await Task.WhenAll(c1.PostAsJsonAsync("/api/caja/abrir",new AbrirCaja(0)),c2.PostAsJsonAsync("/api/caja/abrir",new AbrirCaja(0)));
        var correcta=Assert.Single(resultados,r=>r.IsSuccessStatusCode);
        var s=await Leer<SesionCaja>(correcta);
        try {Assert.Single(resultados,r=>r.StatusCode==HttpStatusCode.Conflict);}
        finally {(await a.PostAsJsonAsync($"/api/caja/{s.Id}/forzar-cierre",new CerrarCaja(null,"Fin de prueba"))).EnsureSuccessStatusCode();}
    }
    [PostgresFact, Trait("Requerimiento","RF-VEN-03")]
    public async Task Si_falla_guardado_se_revierte_venta_pagos_auditoria_y_consecutivo()
    {
        await using var t=await Abrir();var d=t.Solicitud();
        using var scope=f.Api.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();
        var antes=await db.Consecutivos.AsNoTracking().SingleAsync(c=>c.Tipo==TipoDocumento.TIQUETE);
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fallo_cobro_prueba() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Fallo de prueba'; END; $$; CREATE TRIGGER fallo_cobro BEFORE INSERT ON ventas FOR EACH ROW EXECUTE FUNCTION fallo_cobro_prueba();");
        try
        {
            Assert.Equal(HttpStatusCode.Conflict,(await t.Cajero.PostAsJsonAsync("/api/ventas",d)).StatusCode);
            Assert.False(await db.Set<Venta>().AnyAsync(v=>v.Id==d.Id));
            Assert.False(await db.Set<LineaVenta>().AnyAsync(l=>l.VentaId==d.Id));
            Assert.False(await db.Set<PagoVenta>().AnyAsync(p=>p.VentaId==d.Id));
            Assert.False(await db.Bitacoras.AnyAsync(b=>b.EntidadId==d.Id));
            Assert.Equal(antes.SiguienteNumero,(await db.Consecutivos.AsNoTracking().SingleAsync(c=>c.Tipo==TipoDocumento.TIQUETE)).SiguienteNumero);
        }
        finally {await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fallo_cobro ON ventas; DROP FUNCTION fallo_cobro_prueba();");}
        (await t.Cajero.PostAsJsonAsync("/api/ventas",d)).EnsureSuccessStatusCode();
    }
    [PostgresFact, Trait("Requerimiento","RF-CAJ-03")]
    public async Task Administrador_puede_forzar_con_conteo_y_motivo_es_obligatorio()
    {
        await using var t=await Abrir();
        Assert.Equal(HttpStatusCode.BadRequest,(await t.Admin.PostAsJsonAsync($"/api/caja/{t.Sesion.Id}/forzar-cierre",new CerrarCaja(null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await t.Cajero.PostAsJsonAsync($"/api/caja/{t.Sesion.Id}/cerrar",new CerrarCaja(null))).StatusCode);
        var r=await Leer<ReporteCaja>(await t.Admin.PostAsJsonAsync($"/api/caja/{t.Sesion.Id}/forzar-cierre",new CerrarCaja([new(10000,1)],"Cierre por ausencia")));
        Assert.False(r.Sesion.CerradaSinConteo);Assert.Equal(10000m,r.Contado);Assert.Equal(0m,r.Diferencia);
    }
    [PostgresFact, Trait("Requerimiento","RF-CAT-01")]
    public async Task Fotos_locales_aceptan_png_con_limite_y_cajero_no_puede_subir()
    {
        await using var t=await Abrir();var ruta=$"/api/productos/{t.Producto.Id}/foto";
        Assert.Equal(HttpStatusCode.Forbidden,(await t.Cajero.PostAsync(ruta,new ByteArrayContent([1]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await t.Admin.PostAsync(ruta,new ByteArrayContent([1,2]))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,(await t.Admin.PostAsync(ruta,new ByteArrayContent(new byte[1048577]))).StatusCode);
        var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a2ioAAAAASUVORK5CYII=");
        (await t.Admin.PostAsync(ruta,new ByteArrayContent(png))).EnsureSuccessStatusCode();
        var imagen=await t.Cajero.GetAsync(ruta);imagen.EnsureSuccessStatusCode();Assert.Equal("image/png",imagen.Content.Headers.ContentType!.MediaType);
    }
    [PostgresFact, Trait("Requerimiento","RF-VEN-10")]
    public async Task Consulta_restringe_otro_cajero_y_administrador_consulta_sesion_cerrada()
    {
        await using var t=await Abrir();var venta=await Leer<VentaDto>(await t.Cajero.PostAsJsonAsync("/api/ventas",t.Solicitud()));
        var otro=await f.CrearCajero(t.Admin);using var c=await f.Cajero(otro.Id);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync($"/api/ventas/{venta.Id}")).StatusCode);
        (await t.Admin.PostAsJsonAsync($"/api/caja/{t.Sesion.Id}/forzar-cierre",new CerrarCaja(null,"Fin"))).EnsureSuccessStatusCode();
        var historial=await Leer<List<VentaDto>>(await t.Admin.GetAsync($"/api/caja/{t.Sesion.Id}/ventas?numero={venta.Numero}"));
        Assert.Equal(venta.Id,Assert.Single(historial).Id);
        var actual=await t.Cajero.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/caja/actual");Assert.Equal(System.Text.Json.JsonValueKind.Null,actual.GetProperty("sesion").ValueKind);
    }
}
