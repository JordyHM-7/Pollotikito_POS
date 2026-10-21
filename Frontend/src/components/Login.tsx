import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { api } from "../api";
import type { Usuario } from "../api";
import { Campo, ErrorTexto } from "./ui";
export function Login({ entrar }: { entrar: (u: Usuario) => Promise<void> }) {
  const [modo, setModo] = useState("pin"),
    [cuentas, setCuentas] = useState<{ id: string; nombre: string }[]>([]),
    [id, setId] = useState(""),
    [pin, setPin] = useState(""),
    [nombre, setNombre] = useState(""),
    [password, setPassword] = useState(""),
    [error, setError] = useState(""),
    [ocupado, setOcupado] = useState(false);
  useEffect(() => {
    api<typeof cuentas>("/auth/cajeros")
      .then((c) => {
        setCuentas(c);
        setId(c[0]?.id || "");
      })
      .catch((e) => setError(e.message));
  }, []);
  async function enviar(e: FormEvent) {
    e.preventDefault();
    setError("");
    setOcupado(true);
    try {
      const u = await api<Usuario>(
        "/auth/login/" + (modo === "pin" ? "pin" : "admin"),
        "POST",
        modo === "pin"
          ? { usuarioId: id, pin }
          : { nombreUsuario: nombre, password },
      );
      setPin("");
      setPassword("");
      await entrar(u);
    } catch (e) {
      setError((e as Error).message);
      setPin("");
      setPassword("");
    } finally {
      setOcupado(false);
    }
  }
  return (
    <section className="acceso">
      <h1>Iniciar sesión</h1>
      <nav>
        <button
          className={modo === "pin" ? "elegido" : ""}
          onClick={() => {
            setModo("pin");
            setError("");
          }}
        >
          Con PIN
        </button>
        <button
          className={modo === "admin" ? "elegido" : ""}
          onClick={() => {
            setModo("admin");
            setError("");
          }}
        >
          Administrador
        </button>
      </nav>
      <form onSubmit={enviar}>
        <fieldset disabled={ocupado}>
          {modo === "pin" ? (
            <>
              <Campo titulo="Usuario">
                <select
                  value={id}
                  onChange={(e) => setId(e.target.value)}
                  required
                >
                  <option value="" disabled>
                    Seleccioná tu nombre
                  </option>
                  {cuentas.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.nombre}
                    </option>
                  ))}
                </select>
              </Campo>
              <Campo titulo="PIN">
                <input
                  type="password"
                  inputMode="numeric"
                  autoComplete="off"
                  pattern="[0-9]{4,6}"
                  minLength={4}
                  maxLength={6}
                  value={pin}
                  onChange={(e) =>
                    setPin(e.target.value.replace(/\D/g, "").slice(0, 6))
                  }
                  required
                />
              </Campo>
              <div className="teclado">
                {[
                  "1",
                  "2",
                  "3",
                  "4",
                  "5",
                  "6",
                  "7",
                  "8",
                  "9",
                  "Limpiar",
                  "0",
                  "Borrar",
                ].map((n) => (
                  <button
                    type="button"
                    key={n}
                    onClick={() =>
                      setPin((p) =>
                        n === "Limpiar"
                          ? ""
                          : n === "Borrar"
                            ? p.slice(0, -1)
                            : (p + n).slice(0, 6),
                      )
                    }
                  >
                    {n}
                  </button>
                ))}
              </div>
            </>
          ) : (
            <>
              <Campo titulo="Usuario">
                <input
                  autoComplete="username"
                  value={nombre}
                  onChange={(e) => setNombre(e.target.value)}
                  maxLength={80}
                  required
                />
              </Campo>
              <Campo titulo="Contraseña">
                <input
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  maxLength={128}
                  required
                />
              </Campo>
            </>
          )}
          <ErrorTexto texto={error} />
          <button
            className="primario"
            disabled={ocupado || (modo === "pin" && !id)}
          >
            {ocupado ? "Ingresando…" : "Ingresar"}
          </button>
        </fieldset>
      </form>
    </section>
  );
}
export function CambioPassword({
  entrar,
}: {
  entrar: (u: Usuario) => Promise<void>;
}) {
  const [actual, setActual] = useState(""),
    [nueva, setNueva] = useState(""),
    [confirmar, setConfirmar] = useState(""),
    [error, setError] = useState(""),
    [ocupado, setOcupado] = useState(false);
  async function guardar(e: FormEvent) {
    e.preventDefault();
    setError("");
    if (nueva !== confirmar) {
      setError("Las contraseñas nuevas no coinciden.");
      return;
    }
    setOcupado(true);
    try {
      await entrar(
        await api<Usuario>("/auth/cambiar-password", "POST", {
          passwordActual: actual,
          passwordNueva: nueva,
        }),
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setOcupado(false);
    }
  }
  return (
    <section className="acceso">
      <h1>Cambiá tu contraseña</h1>
      <p>
        Para continuar, reemplazá la contraseña inicial o temporal por una
        propia de al menos 10 caracteres.
      </p>
      <form onSubmit={guardar}>
        <fieldset disabled={ocupado}>
          <Campo titulo="Contraseña actual">
            <input
              type="password"
              autoComplete="current-password"
              value={actual}
              onChange={(e) => setActual(e.target.value)}
              maxLength={128}
              required
            />
          </Campo>
          <Campo titulo="Nueva contraseña">
            <input
              type="password"
              autoComplete="new-password"
              value={nueva}
              onChange={(e) => setNueva(e.target.value)}
              minLength={10}
              maxLength={128}
              required
            />
          </Campo>
          <Campo titulo="Confirmar nueva contraseña">
            <input
              type="password"
              autoComplete="new-password"
              value={confirmar}
              onChange={(e) => setConfirmar(e.target.value)}
              minLength={10}
              maxLength={128}
              required
            />
          </Campo>
          <ErrorTexto texto={error} />
          <button className="primario">Guardar y continuar</button>
        </fieldset>
      </form>
    </section>
  );
}
