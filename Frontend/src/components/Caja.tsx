import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { api, ErrorHttp } from "../api";
import {
  centavos,
  moneda,
  nombreMetodo,
  sinDescuento,
  uuidV7,
} from "../comercio";
import type {
  Categoria,
  Producto,
  Descuento,
  TipoDescuento,
  TipoOrden,
  LineaSolicitud,
  Sesion,
  Venta,
  Reporte,
  SolicitudVenta,
  PagoSolicitud,
} from "../comercio";
import { Campo, ErrorTexto } from "./ui";
import type { Menu } from "../menu";
import {
  Personalizar,
  ResumenElecciones,
  tieneElecciones,
} from "./Personalizar";
import {
  CierreForm,
  ReporteSesion,
  Tiquete,
  VentasTurno,
} from "./Comprobantes";

export function Caja() {
  const [sesion, setSesion] = useState<Sesion | null>(null),
    [cargando, setCargando] = useState(true),
    [error, setError] = useState(""),
    [monto, setMonto] = useState(0),
    [busy, setBusy] = useState(false),
    [reporte, setReporte] = useState<Reporte | null>(null);
  async function actualizar() {
    setSesion((await api<{ sesion: Sesion | null }>("/caja/actual")).sesion);
  }
  useEffect(() => {
    actualizar()
      .catch((e) => setError(e.message))
      .finally(() => setCargando(false));
  }, []);
  if (reporte)
    return (
      <ReporteSesion
        reporte={reporte}
        volver={() => {
          setReporte(null);
          void actualizar();
        }}
      />
    );
  if (cargando) return <section>Cargando caja…</section>;
  if (!sesion)
    return (
      <section className="acceso">
        <h1>Abrir caja</h1>
        <p>
          Ingresá el efectivo inicial. Solo puede haber una caja abierta en la
          sucursal.
        </p>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setError("");
            setBusy(true);
            api<Sesion>("/caja/abrir", "POST", { montoInicial: monto })
              .then(setSesion)
              .catch((e) => setError(e.message))
              .finally(() => setBusy(false));
          }}
        >
          <fieldset disabled={busy}>
            <Campo titulo="Monto inicial en efectivo">
              <input
                type="number"
                min={0}
                step="0.01"
                value={monto}
                onChange={(e) => setMonto(Number(e.target.value))}
                required
              />
            </Campo>
            <ErrorTexto texto={error} />
            <button className="primario">Abrir mi caja</button>
            <button
              type="button"
              onClick={() =>
                void actualizar().catch((e) => setError(e.message))
              }
            >
              Actualizar
            </button>
          </fieldset>
        </form>
      </section>
    );
  return (
    <CajaAbierta
      key={sesion.id}
      sesion={sesion}
      cerrado={(r) => {
        setSesion(null);
        setReporte(r);
      }}
    />
  );
}
function DescuentoCampos({
  valor,
  cambiar,
  titulo,
}: {
  valor: Descuento;
  cambiar: (d: Descuento) => void;
  titulo: string;
}) {
  return (
    <div className="descuento-campos">
      <Campo titulo={titulo}>
        <select
          value={valor.tipo}
          onChange={(e) =>
            cambiar({
              tipo: e.target.value as TipoDescuento,
              valor: 0,
              motivo: null,
            })
          }
        >
          <option value="Ninguno">Sin descuento</option>
          <option value="Porcentaje">Porcentaje</option>
          <option value="Monto">Monto fijo</option>
        </select>
      </Campo>
      {valor.tipo !== "Ninguno" && (
        <>
          <Campo titulo={valor.tipo === "Porcentaje" ? "Porcentaje" : "Monto"}>
            <input
              type="number"
              min={0}
              max={valor.tipo === "Porcentaje" ? 100 : undefined}
              step="0.01"
              value={valor.valor}
              onChange={(e) =>
                cambiar({ ...valor, valor: Number(e.target.value) })
              }
              required
            />
          </Campo>
          <Campo titulo="Motivo obligatorio">
            <input
              value={valor.motivo || ""}
              onChange={(e) => cambiar({ ...valor, motivo: e.target.value })}
              maxLength={500}
              required
            />
          </Campo>
        </>
      )}
    </div>
  );
}
function CajaAbierta({
  sesion,
  cerrado,
}: {
  sesion: Sesion;
  cerrado: (r: Reporte) => void;
}) {
  const [catalogo, setCatalogo] = useState<{
      categorias: Categoria[];
      productos: Producto[];
      menu?: Menu;
    }>({ categorias: [], productos: [] }),
    [cat, setCat] = useState(""),
    [lineas, setLineas] = useState<LineaSolicitud[]>([]),
    [descuento, setDescuento] = useState<Descuento>(sinDescuento),
    [tipo, setTipo] = useState<TipoOrden>("ComerAqui"),
    [cotizacion, setCotizacion] = useState<Venta | null>(null),
    [cotizando, setCotizando] = useState(false),
    [error, setError] = useState(""),
    [vista, setVista] = useState("venta"),
    [cobro, setCobro] = useState(false),
    [tiquete, setTiquete] = useState<Venta | null>(null),
    [reporte, setReporte] = useState<Reporte | null>(null);
  const [personalizar, setPersonalizar] = useState<{
    producto: Producto;
    indice?: number;
  } | null>(null);
  const clave = `pollotikito:cobro-pendiente:${sesion.id}`;
  const [pendiente, setPendiente] = useState<SolicitudVenta | null>(() => {
    try {
      return JSON.parse(sessionStorage.getItem(clave) || "null");
    } catch {
      return null;
    }
  });
  const [recuperado, setRecuperado] = useState(!!pendiente);
  useEffect(() => {
    api<typeof catalogo>("/caja/catalogo")
      .then((c) => {
        setCatalogo(c);
        setCat(c.categorias[0]?.id || "");
      })
      .catch((e) => setError(e.message));
  }, []);
  useEffect(() => {
    let activa = true;
    setCotizacion(null);
    setError("");
    if (!lineas.length) {
      setCotizando(false);
      return;
    }
    setCotizando(true);
    const t = setTimeout(() => {
      api<Venta>("/ventas/cotizar", "POST", {
        tipoOrden: tipo,
        lineas,
        descuento,
      })
        .then((v) => {
          if (activa) setCotizacion(v);
        })
        .catch((e) => {
          if (activa) setError(e.message);
        })
        .finally(() => {
          if (activa) setCotizando(false);
        });
    }, 180);
    return () => {
      activa = false;
      clearTimeout(t);
    };
  }, [lineas, descuento, tipo]);
  function recordar(d: SolicitudVenta | null) {
    setPendiente(d);
    try {
      if (d) sessionStorage.setItem(clave, JSON.stringify(d));
      else sessionStorage.removeItem(clave);
    } catch {
      // Si el navegador restringe almacenamiento, el reintento sigue disponible en memoria.
    }
  }
  function vendido(v: Venta) {
    recordar(null);
    setRecuperado(false);
    setTiquete(v);
    setCobro(false);
    setLineas([]);
    setDescuento(sinDescuento);
    setTipo("ComerAqui");
  }
  function agregar(p: Producto) {
    if (tieneElecciones(p, catalogo.menu)) {
      setPersonalizar({ producto: p });
      return;
    }
    setLineas((ls) => {
      const i = ls.findIndex(
        (l) =>
          l.productoId === p.id &&
          !l.nota &&
          l.descuento.tipo === "Ninguno" &&
          !l.modificadores?.length &&
          !l.componentes?.length,
      );
      return i < 0
        ? [
            ...ls,
            {
              productoId: p.id,
              cantidad: 1,
              nota: "",
              descuento: sinDescuento,
            },
          ]
        : ls.map((l, j) => (j === i ? { ...l, cantidad: l.cantidad + 1 } : l));
    });
  }
  function cambiar(i: number, datos: Partial<LineaSolicitud>) {
    setLineas((ls) =>
      ls
        .map((l, j) => (i === j ? { ...l, ...datos } : l))
        .filter((l) => l.cantidad > 0),
    );
  }
  if (tiquete)
    return <Tiquete venta={tiquete} volver={() => setTiquete(null)} />;
  if (reporte)
    return <ReporteSesion reporte={reporte} volver={() => setReporte(null)} />;
  const bloqueado = !!pendiente || cobro;
  return (
    <section className="caja">
      {personalizar && catalogo.menu && (
        <Personalizar
          producto={personalizar.producto}
          menu={catalogo.menu}
          linea={
            personalizar.indice === undefined
              ? undefined
              : lineas[personalizar.indice]
          }
          cancelar={() => setPersonalizar(null)}
          guardar={(l) => {
            if (personalizar.indice === undefined)
              setLineas((ls) => [...ls, l]);
            else cambiar(personalizar.indice, l);
            setPersonalizar(null);
          }}
        />
      )}
      <div className="titulo">
        <h1>Caja — {sesion.nombreCajero}</h1>
        <span>Inicial: {moneda(sesion.montoInicial)}</span>
      </div>
      {pendiente && (
        <p role="alert" className="aviso">
          Hay un cobro pendiente de confirmación. Reintentá exactamente la misma
          solicitud para comprobar el resultado sin duplicar la venta.
        </p>
      )}
      <nav className="no-imprimir">
        <button disabled={!!pendiente} onClick={() => setVista("venta")}>
          Vender
        </button>
        <button disabled={bloqueado} onClick={() => setVista("deposito")}>
          Depósito
        </button>
        <button disabled={bloqueado} onClick={() => setVista("retiro")}>
          Retiro
        </button>
        <button disabled={bloqueado} onClick={() => setVista("ventas")}>
          Ventas del turno
        </button>
        <button disabled={bloqueado} onClick={() => setVista("cierre")}>
          Cerrar caja
        </button>
        <button
          disabled={bloqueado}
          onClick={() => {
            api<Reporte>(`/caja/${sesion.id}/reporte`)
              .then(setReporte)
              .catch((e) => setError(e.message));
          }}
        >
          Reporte
        </button>
      </nav>
      <ErrorTexto texto={error} />
      {vista === "venta" && (
        <>
          <div className="venta-layout">
            <fieldset disabled={bloqueado} className="catalogo-venta">
              <nav aria-label="Categorías">
                {catalogo.categorias.map((c) => (
                  <button
                    key={c.id}
                    className={cat === c.id ? "elegido" : ""}
                    onClick={() => setCat(c.id)}
                  >
                    {c.nombre}
                  </button>
                ))}
              </nav>
              <div className="productos-grid">
                {catalogo.productos
                  .filter((p) => p.categoriaId === cat)
                  .map((p) => (
                    <button
                      key={p.id}
                      className="boton-producto"
                      onClick={() => agregar(p)}
                    >
                      {p.foto && (
                        <img
                          alt=""
                          src={`/api/productos/${p.id}/foto?v=${p.foto}`}
                        />
                      )}
                      <strong>{p.nombre}</strong>
                      <span>{moneda(p.precio)}</span>
                      {p.tipoDescuento !== "Ninguno" && (
                        <small>Promoción según vigencia</small>
                      )}
                    </button>
                  ))}
              </div>
              {!catalogo.productos.length && (
                <p>
                  No hay productos disponibles. Pedile al administrador que
                  configure categorías y productos activos.
                </p>
              )}
            </fieldset>
            <div className="orden">
              <h2>Orden</h2>
              <fieldset disabled={bloqueado}>
                {lineas.length === 0 && <p>Tocá un producto para agregarlo.</p>}
                {lineas.map((l, i) => (
                  <div className="panel linea-orden" key={i}>
                    <div className="fila">
                      <strong>
                        {
                          catalogo.productos.find((p) => p.id === l.productoId)
                            ?.nombre
                        }
                      </strong>
                      <span>
                        {cotizacion?.lineas[i] &&
                          moneda(cotizacion.lineas[i].total)}
                      </span>
                    </div>
                    <div className="acciones cantidad">
                      <button
                        aria-label="Disminuir cantidad"
                        onClick={() => cambiar(i, { cantidad: l.cantidad - 1 })}
                      >
                        −
                      </button>
                      <span>{l.cantidad}</span>
                      <button
                        aria-label="Aumentar cantidad"
                        onClick={() => cambiar(i, { cantidad: l.cantidad + 1 })}
                      >
                        +
                      </button>
                      <button
                        onClick={() =>
                          setLineas((ls) => ls.filter((_, j) => i !== j))
                        }
                      >
                        Quitar
                      </button>
                    </div>
                    <Campo titulo="Nota">
                      <input
                        value={l.nota}
                        onChange={(e) => cambiar(i, { nota: e.target.value })}
                        maxLength={500}
                      />
                    </Campo>
                    <ResumenElecciones linea={l} menu={catalogo.menu} />
                    <div className="acciones">
                      <button
                        onClick={() =>
                          setLineas((ls) => [
                            ...ls,
                            structuredClone({ ...l, cantidad: 1 }),
                          ])
                        }
                      >
                        Repetir
                      </button>
                      {catalogo.productos.find((p) => p.id === l.productoId) &&
                        tieneElecciones(
                          catalogo.productos.find(
                            (p) => p.id === l.productoId,
                          )!,
                          catalogo.menu,
                        ) && (
                          <button
                            onClick={() =>
                              setPersonalizar({
                                producto: catalogo.productos.find(
                                  (p) => p.id === l.productoId,
                                )!,
                                indice: i,
                              })
                            }
                          >
                            Editar elecciones
                          </button>
                        )}
                    </div>
                    <DescuentoCampos
                      titulo="Descuento de línea"
                      valor={l.descuento}
                      cambiar={(d) => cambiar(i, { descuento: d })}
                    />
                  </div>
                ))}
                <Campo titulo="Tipo de orden">
                  <select
                    value={tipo}
                    onChange={(e) => setTipo(e.target.value as TipoOrden)}
                  >
                    <option value="ComerAqui">Comer aquí</option>
                    <option value="ParaLlevar">Para llevar</option>
                    <option value="Express">Express</option>
                  </select>
                </Campo>
                <DescuentoCampos
                  titulo="Descuento global"
                  valor={descuento}
                  cambiar={setDescuento}
                />
              </fieldset>
              <p className="total">
                {cotizando
                  ? "Calculando…"
                  : cotizacion
                    ? moneda(cotizacion.total)
                    : moneda(0)}
              </p>
              {cotizacion && (
                <p>
                  Descuentos: {moneda(cotizacion.descuento)} · IVA:{" "}
                  {moneda(cotizacion.impuesto)}
                </p>
              )}
              <button
                className="primario cobrar"
                disabled={
                  (!cotizacion || cotizando || !lineas.length) && !pendiente
                }
                onClick={() => setCobro(true)}
              >
                {pendiente ? "Recuperar / reintentar cobro" : "Cobrar"}
              </button>
            </div>
          </div>
          {(cobro || recuperado) && (
            <Cobro
              sesion={sesion}
              tipo={tipo}
              lineas={lineas}
              descuento={descuento}
              total={cotizacion?.total || 0}
              pendiente={pendiente}
              recordar={recordar}
              completado={vendido}
              cancelar={() => {
                setCobro(false);
                setRecuperado(false);
                setLineas((actuales) => [...actuales]);
              }}
            />
          )}
        </>
      )}
      {(vista === "deposito" || vista === "retiro") && (
        <MovimientoForm
          key={vista}
          sesion={sesion}
          retiro={vista === "retiro"}
          terminado={() => setVista("venta")}
        />
      )}
      {vista === "ventas" && (
        <VentasTurno sesionId={sesion.id} anulable ver={setTiquete} />
      )}
      {vista === "cierre" && (
        <CierreForm
          sesion={sesion}
          forzado={false}
          terminado={cerrado}
          cancelar={() => setVista("venta")}
        />
      )}
    </section>
  );
}
function Cobro({
  sesion,
  tipo,
  lineas,
  descuento,
  total,
  pendiente,
  recordar,
  completado,
  cancelar,
}: {
  sesion: Sesion;
  tipo: TipoOrden;
  lineas: LineaSolicitud[];
  descuento: Descuento;
  total: number;
  pendiente: SolicitudVenta | null;
  recordar: (d: SolicitudVenta | null) => void;
  completado: (v: Venta) => void;
  cancelar: () => void;
}) {
  const [modo, setModo] = useState("Efectivo"),
    [efectivo, setEfectivo] = useState(total),
    [tarjeta, setTarjeta] = useState(0),
    [sinpe, setSinpe] = useState(0),
    [recibido, setRecibido] = useState(total),
    [autorizacion, setAutorizacion] = useState(""),
    [comprobante, setComprobante] = useState(""),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const candado = useRef(false);
  const aplicado =
    modo === "Efectivo" ? total : modo === "Mixto" ? efectivo : 0;
  const vuelto = centavos(recibido - aplicado);
  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (candado.current) return;
    candado.current = true;
    setBusy(true);
    setError("");
    try {
      let solicitud = pendiente;
      if (!solicitud) {
        let pagos: PagoSolicitud[] = [];
        if (modo === "Efectivo" || (modo === "Mixto" && efectivo > 0))
          pagos.push({
            metodo: "Efectivo",
            monto: aplicado,
            recibido,
            referencia: null,
          });
        if (modo === "Tarjeta" || (modo === "Mixto" && tarjeta > 0))
          pagos.push({
            metodo: "Tarjeta",
            monto: modo === "Tarjeta" ? total : tarjeta,
            recibido: null,
            referencia: autorizacion,
          });
        if (modo === "Sinpe" || (modo === "Mixto" && sinpe > 0))
          pagos.push({
            metodo: "Sinpe",
            monto: modo === "Sinpe" ? total : sinpe,
            recibido: null,
            referencia: comprobante,
          });
        if (centavos(pagos.reduce((a, p) => a + p.monto, 0)) !== total)
          throw new Error("Los montos aplicados deben sumar el total.");
        solicitud = {
          id: uuidV7(),
          sesionCajaId: sesion.id,
          tipoOrden: tipo,
          lineas: structuredClone(lineas),
          descuento: structuredClone(descuento),
          pagos,
        };
        recordar(solicitud);
      }
      completado(await api<Venta>("/ventas", "POST", solicitud));
    } catch (e) {
      setError((e as Error).message);
      if (
        e instanceof ErrorHttp &&
        e.status >= 400 &&
        e.status < 500 &&
        e.status !== 409
      )
        recordar(null);
    } finally {
      setBusy(false);
      candado.current = false;
    }
  }
  async function verificar() {
    if (!pendiente) return;
    setBusy(true);
    try {
      completado(await api<Venta>(`/ventas/${pendiente.id}`));
    } catch (e) {
      setError(
        e instanceof ErrorHttp && e.status === 404
          ? "Todavía no se encontró el cobro. Reintentá la misma solicitud."
          : (e as Error).message,
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="modal-fondo">
      <form
        className="modal"
        onSubmit={enviar}
        role="dialog"
        aria-modal="true"
        aria-labelledby="titulo-cobro"
      >
        <h2 id="titulo-cobro">Cobrar</h2>
        <p className="total">
          {moneda(
            pendiente
              ? pendiente.pagos.reduce((a, p) => a + p.monto, 0)
              : total,
          )}
        </p>
        <fieldset disabled={busy || !!pendiente}>
          <Campo titulo="Método de pago">
            <select
              value={modo}
              onChange={(e) => {
                setModo(e.target.value);
                setEfectivo(total);
                setTarjeta(0);
                setSinpe(0);
              }}
            >
              <option value="Efectivo">Efectivo</option>
              <option value="Tarjeta">Tarjeta</option>
              <option value="Sinpe">SINPE Móvil</option>
              <option value="Mixto">Pago mixto</option>
            </select>
          </Campo>
          {modo === "Mixto" && (
            <div className="grid">
              <Campo titulo="Parte en efectivo">
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  value={efectivo}
                  onChange={(e) => setEfectivo(Number(e.target.value))}
                />
              </Campo>
              <Campo titulo="Parte en tarjeta">
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  value={tarjeta}
                  onChange={(e) => setTarjeta(Number(e.target.value))}
                />
              </Campo>
              <Campo titulo="Parte en SINPE">
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  value={sinpe}
                  onChange={(e) => setSinpe(Number(e.target.value))}
                />
              </Campo>
            </div>
          )}
          {(modo === "Efectivo" || (modo === "Mixto" && efectivo > 0)) && (
            <>
              <Campo titulo="Efectivo recibido">
                <input
                  type="number"
                  min={aplicado}
                  step="0.01"
                  value={recibido}
                  onChange={(e) => setRecibido(Number(e.target.value))}
                  required
                />
              </Campo>
              <div className="acciones billetes">
                {[1000, 2000, 5000, 10000, 20000].map((n) => (
                  <button key={n} type="button" onClick={() => setRecibido(n)}>
                    {moneda(n)}
                  </button>
                ))}
                <button type="button" onClick={() => setRecibido(aplicado)}>
                  Exacto
                </button>
              </div>
              <p className={"total " + (vuelto < 0 ? "error" : "")}>
                Vuelto: {moneda(Math.max(0, vuelto))}
              </p>
            </>
          )}
          {(modo === "Tarjeta" || (modo === "Mixto" && tarjeta > 0)) && (
            <Campo titulo="Autorización del datáfono (obligatoria)">
              <input
                value={autorizacion}
                onChange={(e) => setAutorizacion(e.target.value)}
                maxLength={100}
                required
              />
            </Campo>
          )}
          {(modo === "Sinpe" || (modo === "Mixto" && sinpe > 0)) && (
            <Campo titulo="Comprobante SINPE (obligatorio)">
              <input
                value={comprobante}
                onChange={(e) => setComprobante(e.target.value)}
                maxLength={100}
                required
              />
            </Campo>
          )}
        </fieldset>
        {pendiente && (
          <div className="aviso">
            <p>Solicitud conservada para reintento idéntico:</p>
            {pendiente.pagos.map((p) => (
              <p key={p.metodo}>
                {nombreMetodo(p.metodo)}: {moneda(p.monto)}
                {p.recibido !== null && ` · Recibido: ${moneda(p.recibido)}`}
              </p>
            ))}
          </div>
        )}
        <ErrorTexto texto={error} />
        <div className="acciones">
          <button className="primario" disabled={busy}>
            {busy
              ? "Confirmando…"
              : pendiente
                ? "Reintentar mismo cobro"
                : "Confirmar cobro"}
          </button>
          {pendiente && (
            <button
              type="button"
              disabled={busy}
              onClick={() => void verificar()}
            >
              Consultar resultado
            </button>
          )}
          <button
            type="button"
            disabled={busy || !!pendiente}
            onClick={cancelar}
          >
            Volver a la orden
          </button>
        </div>
      </form>
    </div>
  );
}
function MovimientoForm({
  sesion,
  retiro,
  terminado,
}: {
  sesion: Sesion;
  retiro: boolean;
  terminado: () => void;
}) {
  const [monto, setMonto] = useState(0),
    [motivo, setMotivo] = useState(""),
    [admins, setAdmins] = useState<{ id: string; nombre: string }[]>([]),
    [admin, setAdmin] = useState(""),
    [pin, setPin] = useState(""),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  useEffect(() => {
    if (retiro)
      api<typeof admins>("/caja/administradores")
        .then((a) => {
          setAdmins(a);
          setAdmin(a[0]?.id || "");
        })
        .catch((e) => setError(e.message));
  }, [retiro]);
  return (
    <form
      className="panel"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        setError("");
        try {
          let headers: Record<string, string> | undefined;
          if (retiro) {
            const t = await api<{ token: string }>(
              "/autorizaciones/administrador",
              "POST",
              { administradorId: admin, pin, accion: "retiro_efectivo" },
            );
            setPin("");
            headers = { "X-Autorizacion-Administrador": t.token };
          }
          await api(
            `/caja/${sesion.id}/${retiro ? "retiros" : "depositos"}`,
            "POST",
            { monto, motivo },
            headers,
          );
          terminado();
        } catch (e) {
          setError((e as Error).message);
          setPin("");
        } finally {
          setBusy(false);
        }
      }}
    >
      <h2>{retiro ? "Retiro" : "Depósito"} de efectivo</h2>
      <fieldset disabled={busy}>
        <div className="grid">
          <Campo titulo="Monto">
            <input
              type="number"
              min="0.01"
              step="0.01"
              value={monto}
              onChange={(e) => setMonto(Number(e.target.value))}
              required
            />
          </Campo>
          <Campo titulo="Motivo obligatorio">
            <input
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              maxLength={500}
              required
            />
          </Campo>
        </div>
        {retiro && (
          <>
            <Campo titulo="Administrador que autoriza">
              <select
                value={admin}
                onChange={(e) => setAdmin(e.target.value)}
                required
              >
                <option value="" disabled>
                  Seleccioná
                </option>
                {admins.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.nombre}
                  </option>
                ))}
              </select>
            </Campo>
            <Campo titulo="PIN de administrador">
              <input
                type="password"
                autoComplete="off"
                inputMode="numeric"
                pattern="[0-9]{4,6}"
                minLength={4}
                maxLength={6}
                value={pin}
                onChange={(e) => setPin(e.target.value)}
                required
              />
            </Campo>
            {!admins.length && (
              <p>
                No hay administradores con PIN habilitado. Un administrador debe
                configurar su PIN para autorizar retiros.
              </p>
            )}
          </>
        )}
        <ErrorTexto texto={error} />
        <button className="primario">
          Registrar {retiro ? "retiro" : "depósito"}
        </button>
      </fieldset>
    </form>
  );
}
