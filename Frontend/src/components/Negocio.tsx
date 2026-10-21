import { useEffect, useState } from "react";
import { api } from "../api";
import type { Configuracion, Consecutivo } from "../api";
import { Campo, ErrorTexto } from "./ui";
const campos = [
  ["nombreComercial", "Nombre comercial"],
  ["razonSocial", "Razón social"],
  ["cedula", "Cédula"],
  ["telefono", "Teléfono"],
  ["correo", "Correo"],
  ["direccion", "Dirección"],
  ["pieTiquete", "Pie de tiquete"],
] as const;
export function Negocio() {
  const [config, setConfig] = useState<Configuracion | null>(null),
    [consecutivos, setConsecutivos] = useState<Consecutivo[]>([]),
    [error, setError] = useState(""),
    [mensaje, setMensaje] = useState(""),
    [ocupado, setOcupado] = useState(false),
    [logoVersion, setLogoVersion] = useState(0);
  useEffect(() => {
    Promise.all([
      api<Configuracion>("/configuracion"),
      api<Consecutivo[]>("/consecutivos"),
    ])
      .then(([c, n]) => {
        setConfig(c);
        setConsecutivos(n);
      })
      .catch((e) => setError(e.message));
  }, []);
  async function ejecutar(tarea: () => Promise<unknown>) {
    setError("");
    setMensaje("");
    setOcupado(true);
    try {
      await tarea();
      setMensaje("Cambios guardados.");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setOcupado(false);
    }
  }
  return (
    <section>
      <h1>Negocio y consecutivos</h1>
      <ErrorTexto texto={error} />
      <p role="status">{mensaje}</p>
      {config && (
        <>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void ejecutar(async () =>
                setConfig(
                  await api<Configuracion>("/configuracion", "PUT", config),
                ),
              );
            }}
          >
            <fieldset disabled={ocupado}>
              <div className="grid">
                {campos.map(([campo, titulo]) => (
                  <Campo key={campo} titulo={titulo}>
                    <input
                      type={campo === "correo" ? "email" : "text"}
                      value={config[campo]}
                      required={campo === "nombreComercial"}
                      maxLength={
                        campo === "direccion" || campo === "pieTiquete"
                          ? 1000
                          : campo === "nombreComercial"
                            ? 150
                            : campo === "correo"
                              ? 254
                              : campo === "cedula" || campo === "telefono"
                                ? 30
                                : 200
                      }
                      onChange={(e) =>
                        setConfig({ ...config, [campo]: e.target.value })
                      }
                    />
                  </Campo>
                ))}
                <Campo titulo="IVA por defecto (%)">
                  <input
                    type="number"
                    min="0"
                    max="100"
                    step="0.01"
                    value={config.ivaPorDefecto}
                    onChange={(e) =>
                      setConfig({
                        ...config,
                        ivaPorDefecto: Number(e.target.value),
                      })
                    }
                    required
                  />
                </Campo>
              </div>
              <button className="primario">Guardar negocio</button>
            </fieldset>
          </form>
          <div className="panel">
            <h2>Logo</h2>
            {config.logo && (
              <img
                className="logo"
                alt="Logo del negocio"
                src={"/api/configuracion/logo?v=" + logoVersion}
              />
            )}
            <Campo titulo="PNG o JPG, máximo 1 MB">
              <input
                type="file"
                accept="image/png,image/jpeg"
                disabled={ocupado}
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f)
                    void ejecutar(async () => {
                      if (f.size > 1048576)
                        throw new Error("El logo debe pesar como máximo 1 MB.");
                      const r = await api<{ ruta: string }>(
                        "/configuracion/logo",
                        "POST",
                        f,
                      );
                      setConfig({ ...config, logo: r.ruta });
                      setLogoVersion((v) => v + 1);
                    });
                  e.target.value = "";
                }}
              />
            </Campo>
          </div>
        </>
      )}
      <h2>Consecutivos</h2>
      <p>
        El siguiente número solo puede mantenerse o subir. No se pueden
        recuperar números ya asignados.
      </p>
      {consecutivos.map((c) => (
        <ConsecutivoForm
          key={c.tipo}
          inicial={c}
          ocupado={ocupado}
          guardar={(c) =>
            ejecutar(async () => {
              const nuevo = await api<Consecutivo>(
                "/consecutivos/" + c.tipo,
                "PUT",
                c,
              );
              setConsecutivos((t) =>
                t.map((x) => (x.tipo === nuevo.tipo ? nuevo : x)),
              );
            })
          }
        />
      ))}
    </section>
  );
}
function ConsecutivoForm({
  inicial,
  ocupado,
  guardar,
}: {
  inicial: Consecutivo;
  ocupado: boolean;
  guardar: (c: Consecutivo) => Promise<void>;
}) {
  const [prefijo, setPrefijo] = useState(inicial.prefijo),
    [numero, setNumero] = useState(inicial.siguienteNumero);
  useEffect(() => {
    setPrefijo(inicial.prefijo);
    setNumero(inicial.siguienteNumero);
  }, [inicial]);
  return (
    <form
      className="panel"
      onSubmit={(e) => {
        e.preventDefault();
        void guardar({ tipo: inicial.tipo, prefijo, siguienteNumero: numero });
      }}
    >
      <h3>{inicial.tipo}</h3>
      <fieldset disabled={ocupado}>
        <div className="grid">
          <Campo titulo="Prefijo">
            <input
              value={prefijo}
              maxLength={30}
              onChange={(e) => setPrefijo(e.target.value)}
            />
          </Campo>
          <Campo titulo="Siguiente número">
            <input
              type="number"
              min={inicial.siguienteNumero}
              max={Number.MAX_SAFE_INTEGER}
              step="1"
              value={numero}
              onChange={(e) => setNumero(Number(e.target.value))}
              required
            />
          </Campo>
        </div>
        <button>Guardar consecutivo</button>
      </fieldset>
    </form>
  );
}
