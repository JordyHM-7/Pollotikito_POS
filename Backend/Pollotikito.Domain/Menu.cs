namespace Pollotikito.Domain;

public enum TipoComponente { Fijo, Elegible }
public sealed class GrupoModificador : Entidad
{
    public string Nombre { get; set; } = "";
    public int Minimo { get; set; }
    public int Maximo { get; set; } = 1;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public List<OpcionModificador> Opciones { get; set; } = [];
}
public sealed class OpcionModificador : Entidad
{
    public Guid GrupoModificadorId { get; set; }
    public string Nombre { get; set; } = "";
    public decimal PrecioAdicional { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
public sealed class ProductoGrupoModificador : Entidad
{
    public Guid ProductoId { get; set; }
    public Guid GrupoModificadorId { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
public sealed class ComponenteCombo : Entidad
{
    public Guid ProductoId { get; set; }
    public string Nombre { get; set; } = "";
    public TipoComponente Tipo { get; set; }
    public int Cantidad { get; set; } = 1;
    public Guid? ProductoFijoId { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public List<OpcionComponenteCombo> Opciones { get; set; } = [];
}
public sealed class OpcionComponenteCombo : Entidad
{
    public Guid ComponenteComboId { get; set; }
    public Guid ProductoId { get; set; }
    public decimal Recargo { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
public sealed class ModificadorLineaVenta : Entidad
{
    public Guid LineaVentaId { get; set; }
    public Guid GrupoModificadorId { get; set; }
    public Guid OpcionModificadorId { get; set; }
    public string NombreGrupo { get; set; } = "";
    public string NombreOpcion { get; set; } = "";
    public decimal PrecioAdicional { get; set; }
    public int Orden { get; set; }
}
public sealed class ComponenteLineaVenta : Entidad
{
    public Guid LineaVentaId { get; set; }
    public Guid ComponenteComboId { get; set; }
    public Guid ProductoId { get; set; }
    public string NombreComponente { get; set; } = "";
    public string NombreProducto { get; set; } = "";
    public int CantidadPorUnidad { get; set; }
    public decimal RecargoUnitario { get; set; }
    public int Orden { get; set; }
}
