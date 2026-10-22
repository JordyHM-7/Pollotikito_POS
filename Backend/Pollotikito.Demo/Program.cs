using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pollotikito.Application;
using Pollotikito.Domain;
using Pollotikito.Infrastructure;

// Solo para capturas ficticias; jamás usa ConnectionStrings:Pollotikito ni User Secrets.
var original=Environment.GetEnvironmentVariable("POLLOTIKITO_TEST_CONNECTION")??throw new InvalidOperationException("Definí POLLOTIKITO_TEST_CONNECTION.");
var schema=Environment.GetEnvironmentVariable("POLLOTIKITO_DEMO_SCHEMA")??"";
if(!Regex.IsMatch(schema,"^demo_pollotikito_[a-f0-9]{32}$"))throw new InvalidOperationException("Esquema demo inválido.");
var cadena=new NpgsqlConnectionStringBuilder(original);
if(cadena.Database!="pollotikito_test")throw new InvalidOperationException("Las capturas solo admiten la base exclusiva pollotikito_test.");
await using var conn=new NpgsqlConnection(original);await conn.OpenAsync();
if(args.Contains("limpiar"))
{await using var drop=new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schema} CASCADE",conn);await drop.ExecuteNonQueryAsync();return;}
var password=Environment.GetEnvironmentVariable("POLLOTIKITO_DEMO_PASSWORD")??throw new InvalidOperationException("Falta contraseña efímera de demo.");
var pin=Environment.GetEnvironmentVariable("POLLOTIKITO_DEMO_PIN")??throw new InvalidOperationException("Falta PIN efímero de demo.");
await using(var crear=new NpgsqlCommand($"CREATE SCHEMA {schema}",conn))await crear.ExecuteNonQueryAsync();
try
{
    cadena.SearchPath=schema;cadena.IncludeErrorDetail=false;
    var contexto=new ContextoDemo();
    var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["AdministradorInicial:Usuario"]="admin_demo",["AdministradorInicial:Password"]=password}).Build();
    var servicios=new ServiceCollection().AddSingleton<IConfiguration>(config).AddSingleton<IContextoActual>(contexto).AddSingleton(TimeProvider.System).AgregarPollotikito(cadena.ConnectionString).BuildServiceProvider();
    await using var proveedor=servicios;await RegistroServicios.Inicializar(proveedor,config,true);
    using var scope=proveedor.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PollotikitoDbContext>();
    var secretos=scope.ServiceProvider.GetRequiredService<ISecretos>();
    var admin=await db.Usuarios.SingleAsync();admin.Nombre="Administración Demo";admin.PinHash=secretos.Hash(admin,pin);contexto.UsuarioId=admin.Id;
    var cajero=new Usuario{SucursalId=contexto.SucursalId,Nombre="Caja Demo",NombreUsuario="caja_demo",NombreUsuarioNormalizado="CAJA_DEMO",Rol=Rol.Cajero};cajero.PinHash=secretos.Hash(cajero,pin);db.Add(cajero);
    var negocio=await db.Configuraciones.SingleAsync();negocio.NombreComercial="Pollotikito Demo";negocio.RazonSocial="Restaurante ficticio de demostración";negocio.Cedula="DEMO-000";negocio.Telefono="0000-0000";negocio.Correo="demo@example.invalid";negocio.Direccion="Dirección ficticia, Costa Rica";negocio.PieTiquete="Gracias por tu visita — demostración";
    var categorias=new[]{"Pollo","Acompañamientos","Bebidas","Combos"}.Select((n,i)=>new Categoria{SucursalId=contexto.SucursalId,Nombre=n,Orden=i}).ToArray();db.AddRange(categorias);
    var productos=new List<Producto>();
    Producto P(string nombre,decimal precio,int cat,bool combo=false){var p=new Producto{SucursalId=contexto.SucursalId,CategoriaId=categorias[cat].Id,Nombre=nombre,Precio=precio,TarifaIva=13,EsCombo=combo,Orden=productos.Count};productos.Add(p);db.Add(p);return p;}
    var pieza=P("Pieza de pollo",1500,0);var papas=P("Papas medianas",1200,1);var grandes=P("Papas grandes",2000,1);var ensalada=P("Ensalada grande",1500,1);
    var bebidas=new[]{P("Coca-Cola personal",800,2),P("Fanta personal",800,2),P("Fresca personal",800,2),P("Refresco 1 litro",1400,2)};var dosLitros=P("Refresco 2 litros",2000,2);
    var combo1=P("Combo 1",4500,3,true);var familiar=P("Combo Familiar",14900,3,true);
    GrupoModificador G(string nombre,int min,int max,params (string Nombre,decimal Precio)[] opciones){var g=new GrupoModificador{SucursalId=contexto.SucursalId,Nombre=nombre,Minimo=min,Maximo=max};foreach(var(n,v) in opciones)g.Opciones.Add(new(){SucursalId=contexto.SucursalId,GrupoModificadorId=g.Id,Nombre=n,PrecioAdicional=v,Orden=g.Opciones.Count});db.Add(g);return g;}
    var sin=G("Preferencias",0,3,("Sin cebolla",0),("Sin ensalada",0),("Salsa aparte",0));var extras=G("Extras",0,2,("Extra papas",800),("Extra queso",500));var tipo=G("Tipo de pieza",1,1,("Pecho",0),("Muslo",0),("Ala",0),("Pierna",0));
    foreach(var p in new[]{pieza,combo1,familiar})foreach(var(g,i) in new[]{sin,extras}.Select((g,i)=>(g,i)))db.Add(new ProductoGrupoModificador{SucursalId=contexto.SucursalId,ProductoId=p.Id,GrupoModificadorId=g.Id,Orden=i});
    db.Add(new ProductoGrupoModificador{SucursalId=contexto.SucursalId,ProductoId=pieza.Id,GrupoModificadorId=tipo.Id,Orden=0});
    void Fijo(Producto combo,string nombre,Producto p,int cantidad,int orden)=>db.Add(new ComponenteCombo{SucursalId=contexto.SucursalId,ProductoId=combo.Id,Nombre=nombre,Tipo=TipoComponente.Fijo,ProductoFijoId=p.Id,Cantidad=cantidad,Orden=orden});
    Fijo(combo1,"Pollo",pieza,2,0);Fijo(combo1,"Papas",papas,1,1);
    var refresco=new ComponenteCombo{SucursalId=contexto.SucursalId,ProductoId=combo1.Id,Nombre="Refresco a elegir",Tipo=TipoComponente.Elegible,Cantidad=1,Orden=2};foreach(var b in bebidas)refresco.Opciones.Add(new(){SucursalId=contexto.SucursalId,ComponenteComboId=refresco.Id,ProductoId=b.Id,Recargo=b==bebidas[3]?600:0,Orden=refresco.Opciones.Count});db.Add(refresco);
    Fijo(familiar,"Pollo",pieza,8,0);Fijo(familiar,"Papas",grandes,1,1);Fijo(familiar,"Ensalada",ensalada,1,2);Fijo(familiar,"Refresco",dosLitros,1,3);
    await db.SaveChangesAsync();
    Console.WriteLine(JsonSerializer.Serialize(new{adminId=admin.Id,cajeroId=cajero.Id,comboId=combo1.Id,piezaId=pieza.Id,grupoId=extras.Id,sucursalId=contexto.SucursalId}));
}
catch
{await using var drop=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",conn);await drop.ExecuteNonQueryAsync();throw;}

sealed class ContextoDemo:IContextoActual
{public Guid SucursalId{get;}=Guid.CreateVersion7();public Guid? UsuarioId{get;set;}public string? Ip=>"127.0.0.1";}
