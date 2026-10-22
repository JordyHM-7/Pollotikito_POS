import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { api } from "../api";
import type { Configuracion } from "../api";
import { moneda } from "../comercio";
import type { Categoria, Producto, TipoDescuento } from "../comercio";
import { Campo, ErrorTexto } from "./ui";

export function CatalogoAdmin() {
  const [categorias, setCategorias] = useState<Categoria[]>([]),
    [productos, setProductos] = useState<Producto[]>([]),
    [tab, setTab] = useState("productos"),
    [filtro, setFiltro] = useState(""),
    [error, setError] = useState(""),
    [edicion, setEdicion] = useState<Producto | null | undefined>(),
    [categoria, setCategoria] = useState<Categoria | null | undefined>(),
    [iva, setIva] = useState(13);
  async function cargar() {
    const [c, p, b] = await Promise.all([
      api<Categoria[]>("/categorias"),
      api<Producto[]>("/productos"),
      api<Configuracion>("/configuracion"),
    ]);
    setCategorias(c);
    setProductos(p);
    setIva(b.ivaPorDefecto);
  }
  useEffect(() => {
    cargar().catch((e) => setError(e.message));
  }, []);
  async function desactivar(tipo: string, id: string) {
    if (!confirm("¿Desactivar este registro?")) return;
    try {
      await api(`/${tipo}/${id}/desactivar`, "POST");
      await cargar();
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <section>
      <h1>Catálogo</h1>
      <nav>
        <button
          onClick={() => setTab("productos")}
          className={tab === "productos" ? "elegido" : ""}
        >
          Productos
        </button>
        <button
          onClick={() => setTab("categorias")}
          className={tab === "categorias" ? "elegido" : ""}
        >
          Categorías
        </button>
      </nav>
      <ErrorTexto texto={error} />
      {tab === "categorias" ? (
        <>
          <button className="primario" onClick={() => setCategoria(null)}>
            Crear categoría
          </button>
          {categoria !== undefined && (
            <CategoriaForm
              key={categoria?.id || "nueva"}
              inicial={categoria}
              cancelar={() => setCategoria(undefined)}
              guardar={async () => {
                setCategoria(undefined);
                await cargar();
              }}
            />
          )}
          <div className="tabla">
            <table>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Orden</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {categorias.map((c) => (
                  <tr key={c.id}>
                    <td>{c.nombre}</td>
                    <td>{c.orden}</td>
                    <td>{c.activo ? "Activa" : "Inactiva"}</td>
                    <td>
                      <div className="acciones">
                        <button onClick={() => setCategoria(c)}>
                          Editar / ordenar
                        </button>
                        {c.activo && (
                          <button
                            onClick={() => void desactivar("categorias", c.id)}
                          >
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
        </>
      ) : (
        <>
          <div className="acciones">
            <button className="primario" onClick={() => setEdicion(null)}>
              Crear producto
            </button>
            <Campo titulo="Filtrar por categoría">
              <select
                value={filtro}
                onChange={(e) => setFiltro(e.target.value)}
              >
                <option value="">Todas</option>
                {categorias.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.nombre}
                    {!c.activo ? " (inactiva)" : ""}
                  </option>
                ))}
              </select>
            </Campo>
          </div>
          {edicion !== undefined && (
            <ProductoForm
              key={edicion?.id || "nuevo"}
              inicial={edicion}
              categorias={categorias}
              iva={iva}
              cancelar={() => setEdicion(undefined)}
              guardar={async () => {
                setEdicion(undefined);
                await cargar();
              }}
            />
          )}
          <div className="tabla">
            <table>
              <thead>
                <tr>
                  <th>Producto</th>
                  <th>Categoría</th>
                  <th>Precio (IVA incl.)</th>
                  <th>Descuento</th>
                  <th>Orden / estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {productos
                  .filter((p) => !filtro || p.categoriaId === filtro)
                  .map((p) => (
                    <tr key={p.id}>
                      <td>{p.nombre}</td>
                      <td>
                        {categorias.find((c) => c.id === p.categoriaId)?.nombre}
                      </td>
                      <td>
                        {moneda(p.precio)} ({p.tarifaIva} %)
                      </td>
                      <td>
                        {p.tipoDescuento === "Ninguno"
                          ? "—"
                          : p.tipoDescuento === "Monto"
                            ? moneda(p.valorDescuento)
                            : `${p.valorDescuento} %`}
                      </td>
                      <td>
                        {p.orden} / {p.activo ? "Activo" : "Inactivo"}
                      </td>
                      <td>
                        <div className="acciones">
                          <button onClick={() => setEdicion(p)}>Editar</button>
                          {p.activo && (
                            <button
                              onClick={() => void desactivar("productos", p.id)}
                            >
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
        </>
      )}
    </section>
  );
}
function CategoriaForm({
  inicial,
  cancelar,
  guardar,
}: {
  inicial: Categoria | null;
  cancelar: () => void;
  guardar: () => Promise<void>;
}) {
  const [nombre, setNombre] = useState(inicial?.nombre || ""),
    [orden, setOrden] = useState(inicial?.orden || 0),
    [activo, setActivo] = useState(inicial?.activo ?? true),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  async function enviar(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      await api(
        "/categorias" + (inicial ? "/" + inicial.id : ""),
        inicial ? "PUT" : "POST",
        { nombre, orden, activo },
      );
      await guardar();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form className="panel" onSubmit={enviar}>
      <h2>{inicial ? "Editar" : "Crear"} categoría</h2>
      <fieldset disabled={busy}>
        <div className="grid">
          <Campo titulo="Nombre">
            <input
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              maxLength={120}
              required
            />
          </Campo>
          <Campo titulo="Orden">
            <input
              type="number"
              min={0}
              step={1}
              value={orden}
              onChange={(e) => setOrden(Number(e.target.value))}
              required
            />
          </Campo>
        </div>
        <label className="check">
          <input
            type="checkbox"
            checked={activo}
            onChange={(e) => setActivo(e.target.checked)}
          />
          Activa
        </label>
        <ErrorTexto texto={error} />
        <div className="acciones">
          <button className="primario">Guardar</button>
          <button type="button" onClick={cancelar}>
            Cancelar
          </button>
        </div>
      </fieldset>
    </form>
  );
}
const localDate = (d: string | null) => {
  if (!d) return "";
  const x = new Date(d);
  return new Date(x.getTime() - x.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
};
function ProductoForm({
  inicial,
  categorias,
  iva,
  cancelar,
  guardar,
}: {
  inicial: Producto | null;
  categorias: Categoria[];
  iva: number;
  cancelar: () => void;
  guardar: () => Promise<void>;
}) {
  const [nombre, setNombre] = useState(inicial?.nombre || ""),
    [cat, setCat] = useState(inicial?.categoriaId || categorias[0]?.id || ""),
    [precio, setPrecio] = useState(inicial?.precio ?? 0),
    [tarifa, setTarifa] = useState(inicial?.tarifaIva ?? iva),
    [orden, setOrden] = useState(inicial?.orden || 0),
    [activo, setActivo] = useState(inicial?.activo ?? true),
    [tipo, setTipo] = useState<TipoDescuento>(
      inicial?.tipoDescuento || "Ninguno",
    ),
    [valor, setValor] = useState(inicial?.valorDescuento || 0),
    [desde, setDesde] = useState(localDate(inicial?.descuentoDesde || null)),
    [hasta, setHasta] = useState(localDate(inicial?.descuentoHasta || null)),
    [foto, setFoto] = useState<File | null>(null),
    [id, setId] = useState(inicial?.id || ""),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  async function enviar(e: FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      if (foto && foto.size > 1048576)
        throw new Error("La foto debe pesar como máximo 1 MB.");
      const p = await api<Producto>(
        "/productos" + (id ? "/" + id : ""),
        id ? "PUT" : "POST",
        {
          nombre,
          categoriaId: cat,
          precio,
          tarifaIva: tarifa,
          orden,
          activo,
          tipoDescuento: tipo,
          valorDescuento: tipo === "Ninguno" ? 0 : valor,
          descuentoDesde: desde ? new Date(desde).toISOString() : null,
          descuentoHasta: hasta ? new Date(hasta).toISOString() : null,
        },
      );
      setId(p.id);
      if (foto) await api(`/productos/${p.id}/foto`, "POST", foto);
      await guardar();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <form className="panel" onSubmit={enviar}>
      <h2>{inicial ? "Editar" : "Crear"} producto</h2>
      <fieldset disabled={busy}>
        <div className="grid">
          <Campo titulo="Nombre">
            <input
              value={nombre}
              onChange={(e) => setNombre(e.target.value)}
              maxLength={150}
              required
            />
          </Campo>
          <Campo titulo="Categoría">
            <select
              value={cat}
              onChange={(e) => setCat(e.target.value)}
              required
            >
              <option value="" disabled>
                Seleccioná
              </option>
              {categorias.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.nombre}
                  {!c.activo ? " (inactiva)" : ""}
                </option>
              ))}
            </select>
          </Campo>
          <Campo titulo="Precio con IVA incluido">
            <input
              type="number"
              min={0}
              step="0.01"
              value={precio}
              onChange={(e) => setPrecio(Number(e.target.value))}
              required
            />
          </Campo>
          <Campo titulo="IVA (%)">
            <input
              type="number"
              min={0}
              max={100}
              step="0.01"
              value={tarifa}
              onChange={(e) => setTarifa(Number(e.target.value))}
              required
            />
          </Campo>
          <Campo titulo="Orden">
            <input
              type="number"
              min={0}
              step={1}
              value={orden}
              onChange={(e) => setOrden(Number(e.target.value))}
              required
            />
          </Campo>
          <Campo titulo="Descuento automático por unidad">
            <select
              value={tipo}
              onChange={(e) => setTipo(e.target.value as TipoDescuento)}
            >
              <option value="Ninguno">Sin descuento</option>
              <option value="Porcentaje">Porcentaje</option>
              <option value="Monto">Monto fijo por unidad</option>
            </select>
          </Campo>
          {tipo !== "Ninguno" && (
            <>
              <Campo
                titulo={tipo === "Monto" ? "Monto por unidad" : "Porcentaje"}
              >
                <input
                  type="number"
                  min={0}
                  max={tipo === "Porcentaje" ? 100 : undefined}
                  step="0.01"
                  value={valor}
                  onChange={(e) => setValor(Number(e.target.value))}
                  required
                />
              </Campo>
              <Campo titulo="Desde (opcional, hora local)">
                <input
                  type="datetime-local"
                  value={desde}
                  onChange={(e) => setDesde(e.target.value)}
                />
              </Campo>
              <Campo titulo="Hasta (opcional, hora local)">
                <input
                  type="datetime-local"
                  value={hasta}
                  onChange={(e) => setHasta(e.target.value)}
                />
              </Campo>
            </>
          )}
          <Campo titulo="Foto PNG/JPG, máximo 1 MB">
            <input
              type="file"
              accept="image/png,image/jpeg"
              onChange={(e) => setFoto(e.target.files?.[0] || null)}
            />
          </Campo>
        </div>
        {inicial?.foto && (
          <img
            className="foto-producto"
            alt={inicial.nombre}
            src={`/api/productos/${inicial.id}/foto?v=${inicial.foto}`}
          />
        )}
        <label className="check">
          <input
            type="checkbox"
            checked={activo}
            onChange={(e) => setActivo(e.target.checked)}
          />
          Activo
        </label>
        <ErrorTexto texto={error} />
        <div className="acciones">
          <button className="primario">Guardar producto</button>
          <button type="button" onClick={cancelar}>
            Cancelar
          </button>
        </div>
      </fieldset>
    </form>
  );
}
