import { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { api, renovarCsrf } from "./api";
import type { Usuario } from "./api";
import { Login, CambioPassword } from "./components/Login";
import { Usuarios } from "./components/Usuarios";
import { Negocio } from "./components/Negocio";
import { CatalogoAdmin } from "./components/Catalogo";
import { Caja } from "./components/Caja";
import { SesionesAdmin } from "./components/Comprobantes";
import "./style.css";
import "./caja.css";
function App() {
  const [usuario, setUsuario] = useState<Usuario | null>(null),
    [cargando, setCargando] = useState(true),
    [vista, setVista] = useState("usuarios");
  useEffect(() => {
    api<Usuario>("/auth/me")
      .then(setUsuario)
      .catch(() => {})
      .finally(() => setCargando(false));
    const salir = () => setUsuario(null);
    window.addEventListener("sesion-vencida", salir);
    return () => window.removeEventListener("sesion-vencida", salir);
  }, []);
  async function entrar(u: Usuario) {
    await renovarCsrf();
    setUsuario(u);
  }
  async function salir() {
    try {
      await api("/auth/logout", "POST");
    } finally {
      setUsuario(null);
      await renovarCsrf();
    }
  }
  if (cargando)
    return (
      <main>
        <p>Cargando…</p>
      </main>
    );
  return (
    <>
      <header>
        <strong>Pollotikito</strong>
        {usuario && (
          <div>
            <span>{usuario.nombre}</span>
            <button onClick={() => void salir()}>Cerrar sesión</button>
          </div>
        )}
      </header>
      <main>
        {!usuario ? (
          <Login entrar={entrar} />
        ) : usuario.debeCambiarCredenciales ? (
          <CambioPassword entrar={entrar} />
        ) : usuario.rol === "Cajero" ? (
          <Caja />
        ) : (
          <>
            <nav aria-label="Administración">
              <button
                className={vista === "catalogo" ? "elegido" : ""}
                onClick={() => setVista("catalogo")}
              >
                Catálogo
              </button>
              <button
                className={vista === "cajas" ? "elegido" : ""}
                onClick={() => setVista("cajas")}
              >
                Sesiones de caja
              </button>
              <button
                className={vista === "usuarios" ? "elegido" : ""}
                onClick={() => setVista("usuarios")}
              >
                Usuarios
              </button>
              <button
                className={vista === "configuracion" ? "elegido" : ""}
                onClick={() => setVista("configuracion")}
              >
                Negocio y consecutivos
              </button>
            </nav>
            {vista === "usuarios" ? (
              <Usuarios actual={usuario} />
            ) : vista === "configuracion" ? (
              <Negocio />
            ) : vista === "catalogo" ? (
              <CatalogoAdmin />
            ) : (
              <SesionesAdmin />
            )}
          </>
        )}
      </main>
    </>
  );
}
createRoot(document.getElementById("root")!).render(<App />);
