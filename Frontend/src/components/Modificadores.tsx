import { useEffect, useState } from "react";
import { api } from "../api";
import type { Grupo, Opcion } from "../menu";
import { moneda } from "../comercio";
import { ErrorTexto, Campo } from "./ui";

export function Modificadores() {
  const [grupos, setGrupos] = useState<Grupo[]>([]),
    [grupo, setGrupo] = useState<Grupo | null>(),
    [error, setError] = useState("");
  const cargar = () => api<Grupo[]>("/modificadores/grupos").then(setGrupos);
  useEffect(() => {
    cargar().catch((e) => setError(e.message));
  }, []);
  const guardar = async () => {
    setGrupo(undefined);
    await cargar();
  };
  return (
    <section>
      <h1>Grupos de modificadores</h1>
      <ErrorTexto texto={error} />
      <button onClick={() => setGrupo(null)}>Crear grupo</button>
      {grupo !== undefined && (
        <GrupoForm
          key={grupo?.id ?? "nuevo"}
          grupo={grupo}
          guardar={guardar}
          cancelar={() => setGrupo(undefined)}
        />
      )}
      {grupos.map((g) => (
        <article className="panel" key={g.id}>
          <h2>{g.nombre}</h2>
          <p>
            Mínimo {g.minimo}, máximo {g.maximo} · Orden {g.orden} ·{" "}
            {g.activo ? "Activo" : "Inactivo"}
          </p>
          <button onClick={() => setGrupo(g)}>Editar grupo / opciones</button>
          <ul>
            {g.opciones.map((o) => (
              <li key={o.id}>
                {o.nombre} · {moneda(o.precioAdicional)} · Orden {o.orden} ·{" "}
                {o.activo ? "Activa" : "Inactiva"}
              </li>
            ))}
          </ul>
        </article>
      ))}
    </section>
  );
}
function GrupoForm({
  grupo,
  guardar,
  cancelar,
}: {
  grupo: Grupo | null;
  guardar: () => Promise<void>;
  cancelar: () => void;
}) {
  const [g, setG] = useState(
      grupo ?? { nombre: "", minimo: 0, maximo: 1, orden: 0, activo: true },
    ),
    [opcion, setOpcion] = useState<Opcion | null>(),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  return (
    <div className="panel">
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            await api(
              "/modificadores/grupos" + (grupo ? "/" + grupo.id : ""),
              grupo ? "PUT" : "POST",
              g,
            );
            await guardar();
          } catch (e) {
            setError((e as Error).message);
          } finally {
            setBusy(false);
          }
        }}
      >
        <h2>{grupo ? "Editar" : "Crear"} grupo</h2>
        <fieldset disabled={busy}>
          <Campo titulo="Nombre">
            <input
              required
              maxLength={120}
              value={g.nombre}
              onChange={(e) => setG({ ...g, nombre: e.target.value })}
            />
          </Campo>
          <div className="grid">
            {(["minimo", "maximo", "orden"] as const).map((k) => (
              <Campo
                key={k}
                titulo={
                  { minimo: "Mínimo", maximo: "Máximo", orden: "Orden" }[k]
                }
              >
                <input
                  type="number"
                  required
                  min={k === "maximo" ? 1 : 0}
                  max={k === "minimo" ? g.maximo : undefined}
                  value={g[k]}
                  onChange={(e) => setG({ ...g, [k]: Number(e.target.value) })}
                />
              </Campo>
            ))}
          </div>
          <label className="check">
            <input
              type="checkbox"
              checked={g.activo}
              onChange={(e) => setG({ ...g, activo: e.target.checked })}
            />
            Activo
          </label>
          <ErrorTexto texto={error} />
          <button>Guardar grupo</button>
          <button type="button" onClick={cancelar}>
            Cerrar
          </button>
        </fieldset>
      </form>
      {grupo && (
        <>
          <h3>Opciones</h3>
          {grupo.opciones.map((o) => (
            <button key={o.id} onClick={() => setOpcion(o)}>
              {o.nombre} ({o.activo ? "activa" : "inactiva"})
            </button>
          ))}
          <button onClick={() => setOpcion(null)}>Agregar opción</button>
          {opcion !== undefined && (
            <OpcionForm
              key={opcion?.id ?? "nueva"}
              grupo={grupo.id}
              opcion={opcion}
              guardar={guardar}
            />
          )}
        </>
      )}
    </div>
  );
}
function OpcionForm({
  grupo,
  opcion,
  guardar,
}: {
  grupo: string;
  opcion: Opcion | null;
  guardar: () => Promise<void>;
}) {
  const [o, setO] = useState(
      opcion ?? { nombre: "", precioAdicional: 0, orden: 0, activo: true },
    ),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  return (
    <form
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        setError("");
        try {
          await api(
            `/modificadores/grupos/${grupo}/opciones` +
              (opcion ? "/" + opcion.id : ""),
            opcion ? "PUT" : "POST",
            o,
          );
          await guardar();
        } catch (e) {
          setError((e as Error).message);
        } finally {
          setBusy(false);
        }
      }}
    >
      <fieldset disabled={busy}>
        <Campo titulo="Nombre de opción">
          <input
            required
            maxLength={120}
            value={o.nombre}
            onChange={(e) => setO({ ...o, nombre: e.target.value })}
          />
        </Campo>
        <Campo titulo="Adicional con IVA">
          <input
            type="number"
            required
            min={0}
            step="0.01"
            value={o.precioAdicional}
            onChange={(e) =>
              setO({ ...o, precioAdicional: Number(e.target.value) })
            }
          />
        </Campo>
        <Campo titulo="Orden de opción">
          <input
            type="number"
            required
            min={0}
            value={o.orden}
            onChange={(e) => setO({ ...o, orden: Number(e.target.value) })}
          />
        </Campo>
        <label className="check">
          <input
            type="checkbox"
            checked={o.activo}
            onChange={(e) => setO({ ...o, activo: e.target.checked })}
          />
          Activa
        </label>
        <ErrorTexto texto={error} />
        <button>Guardar opción</button>
      </fieldset>
    </form>
  );
}
