using Pollotikito.Application;
namespace Pollotikito.Api;
public static class EndpointsMenu
{
    public static void Mapear(WebApplication app)
    {
        var a=app.MapGroup("/api").RequireAuthorization("Administrador");
        a.MapGet("/modificadores/grupos",(MenuServicio s)=>s.Grupos());
        a.MapPost("/modificadores/grupos",(DatosGrupo d,MenuServicio s)=>s.GuardarGrupo(null,d));
        a.MapPut("/modificadores/grupos/{id:guid}",(Guid id,DatosGrupo d,MenuServicio s)=>s.GuardarGrupo(id,d));
        a.MapPost("/modificadores/grupos/{id:guid}/desactivar",(Guid id,MenuServicio s)=>s.DesactivarGrupo(id));
        a.MapPost("/modificadores/grupos/{grupo:guid}/opciones",(Guid grupo,DatosOpcion d,MenuServicio s)=>s.GuardarOpcion(grupo,null,d));
        a.MapPut("/modificadores/grupos/{grupo:guid}/opciones/{id:guid}",(Guid grupo,Guid id,DatosOpcion d,MenuServicio s)=>s.GuardarOpcion(grupo,id,d));
        a.MapPost("/modificadores/opciones/{id:guid}/desactivar",(Guid id,MenuServicio s)=>s.DesactivarOpcion(id));
        a.MapGet("/productos/{id:guid}/grupos-modificadores",(Guid id,MenuServicio s)=>s.Asignaciones(id));
        a.MapPut("/productos/{id:guid}/grupos-modificadores",(Guid id,List<AsignarGrupo> d,MenuServicio s)=>s.Asignar(id,d));
        a.MapGet("/productos/{id:guid}/combo",(Guid id,MenuServicio s)=>s.Combo(id));
        a.MapPut("/productos/{id:guid}/combo",(Guid id,GuardarCombo d,MenuServicio s)=>s.GuardarCombo(id,d));
    }
}
