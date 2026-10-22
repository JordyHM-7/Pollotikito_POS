import { useState } from "react";
import type { Producto, LineaSolicitud } from "../comercio";
import { moneda, sinDescuento } from "../comercio";
import type { Menu, EleccionComponente } from "../menu";
import { Campo } from "./ui";
export function tieneElecciones(p: Producto, menu?: Menu) {
  return (
    p.esCombo ||
    !!menu?.asignaciones.some(
      (a) =>
        a.productoId === p.id &&
        a.activo !== false &&
        menu.grupos.some((g) => g.id === a.grupoModificadorId && g.activo),
    )
  );
}
export function Personalizar({
  producto: p,
  menu,
  linea,
  guardar,
  cancelar,
}: {
  producto: Producto;
  menu: Menu;
  linea?: LineaSolicitud;
  guardar: (l: LineaSolicitud) => void;
  cancelar: () => void;
}) {
  const [mods, setMods] = useState<string[]>(linea?.modificadores ?? []);
  const grupos = menu.asignaciones
    .filter((a) => a.productoId === p.id && a.activo !== false)
    .sort((a, b) => a.orden - b.orden)
    .map((a) => menu.grupos.find((g) => g.id === a.grupoModificadorId)!)
    .filter((g) => g?.activo)
    .sort((a, b) => Number(b.minimo > 0) - Number(a.minimo > 0));
  const componentes = menu.componentes
    .filter((c) => c.productoId === p.id && c.activo !== false)
    .sort((a, b) => a.orden - b.orden);
  const [slots, setSlots] = useState<Record<string, string[]>>(() =>
    Object.fromEntries(
      componentes
        .filter((c) => c.tipo === "Elegible")
        .map((c) => {
          const elegidas = (linea?.componentes ?? [])
            .filter((e) => e.componenteId === c.id)
            .flatMap((e) => Array(e.cantidad).fill(e.productoId));
          return [
            c.id!,
            Array.from({ length: c.cantidad }, (_, i) => elegidas[i] ?? ""),
          ];
        }),
    ),
  );
  const productos = menu.productosComponentes ?? [];
  const extras =
    grupos
      .flatMap((g) => g.opciones)
      .filter((o) => mods.includes(o.id))
      .reduce((s, o) => s + o.precioAdicional, 0) +
    componentes.reduce(
      (s, c) =>
        s +
        (slots[c.id!] ?? []).reduce(
          (t, id) =>
            t + (c.opciones.find((o) => o.productoId === id)?.recargo ?? 0),
          0,
        ),
      0,
    );
  const valido =
    grupos.every((g) => {
      const n = g.opciones.filter(
        (o) => o.activo && mods.includes(o.id),
      ).length;
      return n >= g.minimo && n <= g.maximo;
    }) && Object.values(slots).every((ss) => ss.every(Boolean));
  return (
    <div className="modal-fondo">
      <form
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="personalizar-titulo"
        onSubmit={(e) => {
          e.preventDefault();
          if (!valido) return;
          const elecciones: EleccionComponente[] = [];
          for (const [componenteId, ss] of Object.entries(slots))
            for (const productoId of [...new Set(ss)])
              elecciones.push({
                componenteId,
                productoId,
                cantidad: ss.filter((id) => id === productoId).length,
              });
          guardar({
            ...linea,
            productoId: p.id,
            cantidad: linea?.cantidad ?? 1,
            nota: linea?.nota ?? "",
            descuento: linea?.descuento ?? sinDescuento,
            modificadores: mods,
            componentes: elecciones,
          });
        }}
      >
        <h2 id="personalizar-titulo">Personalizar {p.nombre}</h2>
        <p>
          Elecciones por una unidad. Para elecciones distintas, agregá otra
          línea.
        </p>
        {grupos.map((g) => (
          <fieldset key={g.id}>
            <legend>
              {g.nombre} · {g.minimo > 0 ? "Obligatorio" : "Opcional"} (
              {g.minimo}–{g.maximo})
            </legend>
            {g.opciones
              .filter((o) => o.activo)
              .sort((a, b) => a.orden - b.orden)
              .map((o) => (
                <label className="check" key={o.id}>
                  <input
                    type="checkbox"
                    checked={mods.includes(o.id)}
                    disabled={
                      !mods.includes(o.id) &&
                      g.opciones.filter((o) => mods.includes(o.id)).length >=
                        g.maximo
                    }
                    onChange={(e) =>
                      setMods(
                        e.target.checked
                          ? [...mods, o.id]
                          : mods.filter((id) => id !== o.id),
                      )
                    }
                  />
                  {o.nombre} · +{moneda(o.precioAdicional)}
                </label>
              ))}
          </fieldset>
        ))}
        {componentes.map((c) => (
          <fieldset key={c.id}>
            <legend>
              {c.nombre} · {c.cantidad} por combo
            </legend>
            {c.tipo === "Fijo" ? (
              <p>
                {c.cantidad} ×{" "}
                {productos.find((p) => p.id === c.productoFijoId)?.nombre}
              </p>
            ) : (
              (slots[c.id!] ?? []).map((v, i) => (
                <Campo key={i} titulo={`${c.nombre} ${i + 1}`}>
                  <select
                    required
                    value={v}
                    onChange={(e) =>
                      setSlots({
                        ...slots,
                        [c.id!]: slots[c.id!].map((x, j) =>
                          i === j ? e.target.value : x,
                        ),
                      })
                    }
                  >
                    <option value="">Seleccioná</option>
                    {c.opciones
                      .filter(
                        (o) =>
                          o.activo !== false &&
                          productos.some((p) => p.id === o.productoId),
                      )
                      .sort((a, b) => a.orden - b.orden)
                      .map((o) => (
                        <option key={o.productoId} value={o.productoId}>
                          {productos.find((p) => p.id === o.productoId)?.nombre}{" "}
                          · +{moneda(o.recargo)}
                        </option>
                      ))}
                  </select>
                </Campo>
              ))
            )}
          </fieldset>
        ))}
        <p className="total">
          Unitario antes de descuentos: {moneda(p.precio + extras)}
        </p>
        <p>
          El servidor confirma precios y disponibilidad al cotizar y cobrar.
        </p>
        <button className="primario" disabled={!valido}>
          Confirmar elecciones
        </button>
        <button type="button" onClick={cancelar}>
          Cancelar
        </button>
      </form>
    </div>
  );
}
export function ResumenElecciones({
  linea,
  menu,
}: {
  linea: LineaSolicitud;
  menu?: Menu;
}) {
  if (!menu) return null;
  return (
    <div className="detalle-elecciones">
      {menu.grupos
        .flatMap((g) => g.opciones)
        .filter((o) => linea.modificadores?.includes(o.id))
        .map((o) => (
          <p key={o.id}>↳ {o.nombre}</p>
        ))}
      {linea.componentes?.map((e) => (
        <p key={e.componenteId + e.productoId}>
          ↳ {e.cantidad} ×{" "}
          {menu.productosComponentes.find((p) => p.id === e.productoId)?.nombre}
        </p>
      ))}
    </div>
  );
}
