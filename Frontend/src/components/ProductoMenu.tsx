import { useEffect, useState } from "react";
import { api } from "../api";
import type { Grupo, Asignacion, Componente } from "../menu";
import type { Producto } from "../comercio";
import { Campo, ErrorTexto } from "./ui";
export function ProductoMenu({
  producto,
  productos,
  actualizar,
}: {
  producto: Producto;
  productos: Producto[];
  actualizar: () => Promise<void>;
}) {
  const [grupos, setGrupos] = useState<Grupo[]>([]),
    [asignaciones, setAsignaciones] = useState<Asignacion[]>([]),
    [componentes, setComponentes] = useState<Componente[]>([]),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [cargando, setCargando] = useState(true),
    [mensaje, setMensaje] = useState("");
  useEffect(() => {
    Promise.all([
      api<Grupo[]>("/modificadores/grupos"),
      api<Asignacion[]>(`/productos/${producto.id}/grupos-modificadores`),
      producto.esCombo
        ? api<Componente[]>(`/productos/${producto.id}/combo`)
        : Promise.resolve([]),
    ])
      .then(([g, a, c]) => {
        setGrupos(g);
        setAsignaciones(a.filter((a) => a.activo !== false));
        setComponentes(
          c.map((componente) => ({
            ...componente,
            opciones: componente.opciones.filter((o) => o.activo !== false),
          })),
        );
        setCargando(false);
      })
      .catch((e) => setError(e.message));
  }, [producto.id, producto.esCombo]);
  const simples = productos.filter((p) => !p.esCombo && p.id !== producto.id);
  const cambiar = (i: number, d: Partial<Componente>) =>
    setComponentes((cs) => cs.map((c, j) => (i === j ? { ...c, ...d } : c)));
  return (
    <form
      className="panel"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        setError("");
        setMensaje("");
        try {
          await api(
            `/productos/${producto.id}/grupos-modificadores`,
            "PUT",
            asignaciones,
          );
          if (producto.esCombo)
            await api(`/productos/${producto.id}/combo`, "PUT", {
              componentes,
            });
          await actualizar();
          setMensaje("Elecciones guardadas.");
        } catch (e) {
          setError((e as Error).message);
        } finally {
          setBusy(false);
        }
      }}
    >
      <h2>Elecciones de {producto.nombre}</h2>
      <fieldset disabled={busy || cargando}>
        <h3>Grupos compartidos</h3>
        {grupos.map((g) => {
          const a = asignaciones.find((a) => a.grupoModificadorId === g.id);
          return (
            <div key={g.id} className="acciones">
              <label className="check">
                <input
                  type="checkbox"
                  checked={!!a}
                  onChange={(e) =>
                    setAsignaciones(
                      e.target.checked
                        ? [
                            ...asignaciones,
                            {
                              grupoModificadorId: g.id,
                              orden: asignaciones.length,
                            },
                          ]
                        : asignaciones.filter(
                            (a) => a.grupoModificadorId !== g.id,
                          ),
                    )
                  }
                />
                {g.nombre} ({g.minimo}–{g.maximo})
                {!g.activo ? " · Inactivo" : ""}
              </label>
              {a && (
                <Campo titulo="Orden en producto">
                  <input
                    type="number"
                    min={0}
                    required
                    value={a.orden}
                    onChange={(e) =>
                      setAsignaciones(
                        asignaciones.map((x) =>
                          x === a ? { ...x, orden: Number(e.target.value) } : x,
                        ),
                      )
                    }
                  />
                </Campo>
              )}
            </div>
          );
        })}
        {producto.esCombo && (
          <>
            <h3>Componentes por una unidad de combo</h3>
            {componentes.map((c, i) => (
              <div className="panel" key={c.id ?? i}>
                <Campo titulo="Nombre del componente">
                  <input
                    required
                    maxLength={120}
                    value={c.nombre}
                    onChange={(e) => cambiar(i, { nombre: e.target.value })}
                  />
                </Campo>
                <div className="grid">
                  <Campo titulo="Tipo">
                    <select
                      value={c.tipo}
                      onChange={(e) =>
                        cambiar(i, {
                          tipo: e.target.value as Componente["tipo"],
                          productoFijoId: null,
                          opciones: [],
                        })
                      }
                    >
                      <option value="Fijo">Fijo</option>
                      <option value="Elegible">Elegible</option>
                    </select>
                  </Campo>
                  <Campo titulo="Cantidad por combo">
                    <input
                      type="number"
                      min={1}
                      max={100}
                      required
                      value={c.cantidad}
                      onChange={(e) =>
                        cambiar(i, { cantidad: Number(e.target.value) })
                      }
                    />
                  </Campo>
                  <Campo titulo="Orden de componente">
                    <input
                      type="number"
                      min={0}
                      required
                      value={c.orden}
                      onChange={(e) =>
                        cambiar(i, { orden: Number(e.target.value) })
                      }
                    />
                  </Campo>
                </div>
                {c.tipo === "Fijo" ? (
                  <Campo titulo="Producto fijo">
                    <select
                      required
                      value={c.productoFijoId ?? ""}
                      onChange={(e) =>
                        cambiar(i, { productoFijoId: e.target.value })
                      }
                    >
                      <option value="">Seleccioná</option>
                      {simples.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.nombre}
                          {!p.activo ? " (inactivo)" : ""}
                        </option>
                      ))}
                    </select>
                  </Campo>
                ) : (
                  <>
                    <p>Seleccioná las alternativas y sus recargos con IVA.</p>
                    {simples.map((p) => {
                      const o = c.opciones.find((o) => o.productoId === p.id);
                      return (
                        <div className="acciones" key={p.id}>
                          <label className="check">
                            <input
                              type="checkbox"
                              checked={!!o}
                              onChange={(e) =>
                                cambiar(i, {
                                  opciones: e.target.checked
                                    ? [
                                        ...c.opciones,
                                        {
                                          productoId: p.id,
                                          recargo: 0,
                                          orden: c.opciones.length,
                                        },
                                      ]
                                    : c.opciones.filter(
                                        (x) => x.productoId !== p.id,
                                      ),
                                })
                              }
                            />
                            {p.nombre}
                            {!p.activo ? " (inactivo)" : ""}
                          </label>
                          {o && (
                            <>
                              <Campo titulo="Recargo">
                                <input
                                  type="number"
                                  min={0}
                                  step="0.01"
                                  required
                                  value={o.recargo}
                                  onChange={(e) =>
                                    cambiar(i, {
                                      opciones: c.opciones.map((x) =>
                                        x === o
                                          ? {
                                              ...x,
                                              recargo: Number(e.target.value),
                                            }
                                          : x,
                                      ),
                                    })
                                  }
                                />
                              </Campo>
                              <Campo titulo="Orden de alternativa">
                                <input
                                  type="number"
                                  min={0}
                                  required
                                  value={o.orden}
                                  onChange={(e) =>
                                    cambiar(i, {
                                      opciones: c.opciones.map((x) =>
                                        x === o
                                          ? {
                                              ...x,
                                              orden: Number(e.target.value),
                                            }
                                          : x,
                                      ),
                                    })
                                  }
                                />
                              </Campo>
                            </>
                          )}
                        </div>
                      );
                    })}
                  </>
                )}
                <button
                  type="button"
                  onClick={() =>
                    setComponentes(componentes.filter((_, j) => j !== i))
                  }
                >
                  Quitar componente
                </button>
              </div>
            ))}
            <button
              type="button"
              onClick={() =>
                setComponentes([
                  ...componentes,
                  {
                    nombre: "",
                    tipo: "Fijo",
                    cantidad: 1,
                    productoFijoId: null,
                    orden: componentes.length,
                    opciones: [],
                  },
                ])
              }
            >
              Agregar componente
            </button>
          </>
        )}
        <ErrorTexto texto={error} />
        {mensaje && <p role="status">{mensaje}</p>}
        <button className="primario">Guardar elecciones</button>
      </fieldset>
    </form>
  );
}
