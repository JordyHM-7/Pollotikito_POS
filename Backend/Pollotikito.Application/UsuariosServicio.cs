using Pollotikito.Domain;

namespace Pollotikito.Application;

public sealed class UsuariosServicio(IAlmacen db, ISecretos secretos, IContextoActual contexto)
{
    public async Task<List<UsuarioDto>> Listar() => (await db.Usuarios()).Select(UsuarioDto.De).ToList();
    public async Task<UsuarioDto?> Obtener(Guid id) => await db.Usuario(id) is { } u ? UsuarioDto.De(u) : null;
    public Task<UsuarioDto> Crear(CrearUsuario datos) => db.Transaccion(async () =>
    {
        Validacion.Rol(datos.Rol);
        var nombre = Validacion.Texto(datos.Nombre, "Nombre", 120, true);
        var login = Validacion.Texto(datos.NombreUsuario, "Usuario", 80, true);
        Validacion.Exigir(await db.PorNombre(login.ToUpperInvariant()) == null, "El nombre de usuario ya existe.", 409);
        if (datos.Pin != null) Validacion.Pin(datos.Pin);
        if (datos.Password != null) Validacion.Password(datos.Password);
        Validacion.Exigir(datos.Rol != Rol.Cajero || datos.Pin != null, "El cajero requiere PIN.");
        Validacion.Exigir(datos.Rol != Rol.Administrador || datos.Password != null, "El administrador requiere contraseña.");
        var u = new Usuario { SucursalId = contexto.SucursalId, Nombre = nombre, NombreUsuario = login,
            NombreUsuarioNormalizado = login.ToUpperInvariant(), Rol = datos.Rol };
        if (datos.Password != null) u.PasswordHash = secretos.Hash(u, datos.Password);
        if (datos.Pin != null) u.PinHash = secretos.Hash(u, datos.Pin);
        db.Agregar(u); await db.Guardar(); return UsuarioDto.De(u);
    });

    public Task<UsuarioDto> Editar(Guid id, EditarUsuario datos) => db.Transaccion(async () =>
    {
        var u = await Requerido(id);
        Validacion.Rol(datos.Rol);
        var login = Validacion.Texto(datos.NombreUsuario, "Usuario", 80, true);
        var existente = await db.PorNombre(login.ToUpperInvariant());
        Validacion.Exigir(existente == null || existente.Id == id, "El nombre de usuario ya existe.", 409);
        if (u.Rol == Rol.Administrador && datos.Rol != u.Rol) await ProtegerUltimo(u);
        Validacion.Exigir(datos.Rol != Rol.Administrador || u.PasswordHash != null, "Asigná una contraseña antes de cambiar a Administrador.");
        Validacion.Exigir(datos.Rol != Rol.Cajero || u.PinHash != null, "Asigná un PIN antes de cambiar a Cajero.");
        u.Nombre = Validacion.Texto(datos.Nombre, "Nombre", 120, true);
        u.NombreUsuario = login; u.NombreUsuarioNormalizado = login.ToUpperInvariant();
        u.Rol = datos.Rol; u.VersionSeguridad = Guid.CreateVersion7();
        await db.Guardar(); return UsuarioDto.De(u);
    });

    public Task<bool> Desactivar(Guid id) => db.Transaccion(async () =>
    {
        var u = await Requerido(id);
        Validacion.Exigir(u.Id != contexto.UsuarioId, "No podés desactivar tu propia cuenta.");
        await ProtegerUltimo(u);
        u.Activo = false; u.VersionSeguridad = Guid.CreateVersion7(); await db.Guardar(); return true;
    });

    public Task<UsuarioDto> Credenciales(Guid id, Credenciales datos) => db.Transaccion(async () =>
    {
        var u = await Requerido(id);
        Validacion.Exigir(datos.Password != null || datos.Pin != null || datos.QuitarPin, "Indicá las credenciales a cambiar.");
        Validacion.Exigir(!(datos.QuitarPin && datos.Pin != null), "No se puede asignar y quitar el PIN a la vez.");
        if (datos.Password != null)
        {
            Validacion.Password(datos.Password); u.PasswordHash = secretos.Hash(u, datos.Password);
            if (u.Rol == Rol.Administrador) u.DebeCambiarCredenciales = true;
        }
        if (datos.Pin != null) { Validacion.Pin(datos.Pin); u.PinHash = secretos.Hash(u, datos.Pin); }
        if (datos.QuitarPin) { Validacion.Exigir(u.Rol != Rol.Cajero, "Un cajero necesita PIN."); u.PinHash = null; }
        u.VersionSeguridad = Guid.CreateVersion7(); await db.Guardar(); return UsuarioDto.De(u);
    });

    private async Task<Usuario> Requerido(Guid id) => await db.Usuario(id) ?? throw new ErrorAplicacion(404, "Usuario no encontrado.");
    private async Task ProtegerUltimo(Usuario u)
    {
        if (u.Activo && u.Rol == Rol.Administrador)
            Validacion.Exigir((await db.Usuarios()).Any(x => x.Activo && x.Rol == Rol.Administrador && x.Id != u.Id), "Debe quedar al menos un administrador activo.");
    }
}
