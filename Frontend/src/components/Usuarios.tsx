import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { api } from "../api";
import type { Usuario } from "../api";
import { Campo, ErrorTexto } from "./ui";
export function Usuarios({ actual }: { actual: Usuario }) {
  const [usuarios, setUsuarios] = useState<Usuario[]>([]),
    [editar, setEditar] = useState<Usuario | null | undefined>(undefined),
    [error, setError] = useState(""),
    [credencial, setCredencial] = useState<Usuario | null>(null);
  async function cargar() {
    setUsuarios(await api<Usuario[]>("/usuarios"));
  }
  useEffect(() => {
    cargar().catch((e) => setError(e.message));
  }, []);
  async function desactivar(u: Usuario) {
    if (!confirm(`¿Desactivar a ${u.nombre}?`)) return;
    try {
      await api(`/usuarios/${u.id}/desactivar`, "POST");
      await cargar();
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <section>
      <div className="titulo">
        <h1>Usuarios</h1>
        <button
          className="primario"
          onClick={() => {
            setEditar(null);
            setCredencial(null);
          }}
        >
          Crear usuario
        </button>
      </div>
      <ErrorTexto texto={error} />
      {editar !== undefined && (
        <UsuarioForm
          key={editar?.id || "nuevo"}
          usuario={editar}
          cerrar={() => setEditar(undefined)}
          guardado={async () => {
            setEditar(undefined);
            await cargar();
          }}
        />
      )}
      {credencial && (
        <CredencialesForm
          usuario={credencial}
          cerrar={() => setCredencial(null)}
          guardado={async () => {
            setCredencial(null);
            await cargar();
          }}
        />
      )}
      <div className="tabla">
        <table>
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Usuario</th>
              <th>Rol</th>
              <th>Estado</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {usuarios.map((u) => (
              <tr key={u.id}>
                <td>{u.nombre}</td>
                <td>{u.nombreUsuario}</td>
                <td>{u.rol}</td>
                <td>{u.activo ? "Activo" : "Inactivo"}</td>
                <td>
                  <div className="acciones">
                    <button
                      onClick={() => {
                        setEditar(u);
                        setCredencial(null);
                      }}
                    >
                      Editar
                    </button>
                    <button
                      onClick={() => {
                        setCredencial(u);
                        setEditar(undefined);
                      }}
                    >
                      Credenciales
                    </button>
                    {u.activo && u.id !== actual.id && (
                      <button onClick={() => void desactivar(u)}>
                        Desactivar
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
function UsuarioForm({
  usuario,
  cerrar,
  guardado,
}: {
  usuario: Usuario | null;
  cerrar: () => void;
  guardado: () => Promise<void>;
}) {
  const [nombre, setNombre] = useState(usuario?.nombre || ""),
    [login, setLogin] = useState(usuario?.nombreUsuario || ""),
    [rol, setRol] = useState(usuario?.rol || "Cajero"),
    [password, setPassword] = useState(""),
    [pin, setPin] = useState(""),
    [error, setError] = useState(""),
    [ocupado, setOcupado] = useState(false);
  async function guardar(e: FormEvent) {
    e.preventDefault();
    setError("");
    setOcupado(true);
    try {
      await api(
        "/usuarios" + (usuario ? "/" + usuario.id : ""),
        usuario ? "PUT" : "POST",
        {
          nombre,
          nombreUsuario: login,
          rol,
          ...(!usuario ? { password: password || null, pin: pin || null } : {}),
        },
      );
      setPin("");
      setPassword("");
      await guardado();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setOcupado(false);
    }
  }
  return (
    <form className="panel" onSubmit={guardar}>
      <h2>{usuario ? "Editar usuario" : "Crear usuario"}</h2>
      <fieldset disabled={ocupado}>
        <div className="grid">
          <Campo titulo="Nombre">
            <input
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              maxLength={120}
              required
            />
          </Campo>
          <Campo titulo="Nombre de usuario">
            <input
              value={login}
              onChange={(e) => setLogin(e.target.value)}
              maxLength={80}
              required
            />
          </Campo>
          <Campo titulo="Rol">
            <select
              value={rol}
              onChange={(e) => setRol(e.target.value as Usuario["rol"])}
            >
              <option>Cajero</option>
              <option>Administrador</option>
            </select>
          </Campo>
          {!usuario && (
            <>
              <Campo
                titulo={
                  rol === "Cajero" ? "PIN (obligatorio)" : "PIN (opcional)"
                }
              >
                <input
                  type="password"
                  inputMode="numeric"
                  autoComplete="new-password"
                  value={pin}
                  onChange={(e) => setPin(e.target.value)}
                  pattern="[0-9]{4,6}"
                  minLength={4}
                  maxLength={6}
                  required={rol === "Cajero"}
                />
              </Campo>
              <Campo
                titulo={
                  rol === "Administrador"
                    ? "Contraseña (obligatoria)"
                    : "Contraseña (opcional)"
                }
              >
                <input
                  type="password"
                  autoComplete="new-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  minLength={10}
                  maxLength={128}
                  required={rol === "Administrador"}
                />
              </Campo>
            </>
          )}
        </div>
        <ErrorTexto texto={error} />
        <div className="acciones">
          <button className="primario">Guardar</button>
          <button type="button" onClick={cerrar}>
            Cancelar
          </button>
        </div>
      </fieldset>
    </form>
  );
}
function CredencialesForm({
  usuario,
  cerrar,
  guardado,
}: {
  usuario: Usuario;
  cerrar: () => void;
  guardado: () => Promise<void>;
}) {
  const [password, setPassword] = useState(""),
    [pin, setPin] = useState(""),
    [quitarPin, setQuitarPin] = useState(false),
    [error, setError] = useState(""),
    [ocupado, setOcupado] = useState(false);
  async function guardar(e: FormEvent) {
    e.preventDefault();
    setError("");
    setOcupado(true);
    try {
      await api(`/usuarios/${usuario.id}/credenciales`, "PUT", {
        password: password || null,
        pin: pin || null,
        quitarPin,
      });
      setPin("");
      setPassword("");
      await guardado();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setOcupado(false);
    }
  }
  return (
    <form className="panel" onSubmit={guardar}>
      <h2>Credenciales de {usuario.nombre}</h2>
      <p>
        Completá solo lo que querés cambiar. Una contraseña temporal obliga al
        administrador a reemplazarla al ingresar. El cambio cierra sus sesiones.
      </p>
      <fieldset disabled={ocupado}>
        <div className="grid">
          <Campo titulo="Nueva contraseña">
            <input
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              minLength={10}
              maxLength={128}
            />
          </Campo>
          <Campo titulo="Nuevo PIN">
            <input
              type="password"
              autoComplete="new-password"
              inputMode="numeric"
              value={pin}
              onChange={(e) => setPin(e.target.value)}
              pattern="[0-9]{4,6}"
              minLength={4}
              maxLength={6}
              disabled={quitarPin}
            />
          </Campo>
        </div>
        {usuario.rol === "Administrador" && (
          <label className="check">
            <input
              type="checkbox"
              checked={quitarPin}
              onChange={(e) => {
                setQuitarPin(e.target.checked);
                setPin("");
              }}
            />{" "}
            Quitar PIN
          </label>
        )}
        <ErrorTexto texto={error} />
        <div className="acciones">
          <button className="primario">Guardar credenciales</button>
          <button type="button" onClick={cerrar}>
            Cancelar
          </button>
        </div>
      </fieldset>
    </form>
  );
}
