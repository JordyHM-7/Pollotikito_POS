using Pollotikito.Domain;
namespace Pollotikito.Application;

public interface IMenuRepositorio
{
    Task<List<GrupoModificador>> Grupos();
    Task<GrupoModificador?> Grupo(Guid id);
    Task<OpcionModificador?> Opcion(Guid id);
    Task<List<ProductoGrupoModificador>> Asignaciones();
    Task<List<ComponenteCombo>> Componentes();
    Task<ComponenteCombo?> Componente(Guid id);
}
public sealed record DatosGrupo(string Nombre, int Minimo, int Maximo, int Orden = 0, bool Activo = true);
public sealed record DatosOpcion(string Nombre, decimal PrecioAdicional, int Orden = 0, bool Activo = true);
public sealed record AsignarGrupo(Guid GrupoModificadorId, int Orden);
public sealed record DatosAlternativa(Guid ProductoId, decimal Recargo, int Orden = 0);
public sealed record DatosComponente(Guid? Id, string Nombre, TipoComponente Tipo, int Cantidad, Guid? ProductoFijoId, int Orden, List<DatosAlternativa> Opciones);
public sealed record GuardarCombo(List<DatosComponente> Componentes);
public sealed record EleccionComponente(Guid ComponenteId, Guid ProductoId, int Cantidad);
public sealed record DefinicionMenu(List<GrupoModificador> Grupos, List<ProductoGrupoModificador> Asignaciones, List<ComponenteCombo> Componentes, List<Producto>? ProductosComponentes = null);
