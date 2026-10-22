import { useEffect, useState } from "react";
import { api } from "../api";
import {
  denominaciones,
  moneda,
  fecha,
  nombreMetodo,
  nombreOrden,
} from "../comercio";
import type { Reporte, Venta, Sesion, ResumenMetodo, Linea } from "../comercio";
import { Campo, ErrorTexto } from "./ui";
function DetalleElecciones({ linea: l }: { linea: Linea }) {
  return (
    <div className="detalle-elecciones">
      {l.modificadores?.map((m, i) => (
        <p key={"m" + i}>
          ↳ {m.nombreGrupo}: {m.nombreOpcion} (+{moneda(m.precioAdicional)} por
          unidad)
        </p>
      ))}
      {l.componentes?.map((c, i) => (
        <p key={"c" + i}>
          ↳ {c.nombreComponente}: {c.cantidadPorUnidad} × {c.nombreProducto}
          {c.recargoUnitario > 0
            ? ` (+${moneda(c.recargoUnitario)} cada uno)`
            : ""}{" "}
          por unidad
        </p>
      ))}
    </div>
  );
}

export function Tiquete({
  venta,
  volver,
}: {
  venta: Venta;
  volver: () => void;
}) {
  return (
    <section className="vista-comprobante">
      <div className="acciones no-imprimir">
        <button className="primario" onClick={() => window.print()}>
          Imprimir / guardar PDF
        </button>
        <button onClick={volver}>Volver / Nueva venta</button>
      </div>
      <article className="imprimible tiquete">
        <h1>{venta.negocioNombre}</h1>
        {venta.tieneLogo && (
          <img
            className="logo"
            src={`/api/ventas/${venta.id}/logo`}
            alt="Logo del negocio"
          />
        )}
        <p>
          {[
            venta.negocioRazonSocial,
            venta.negocioCedula && `Cédula: ${venta.negocioCedula}`,
            venta.negocioDireccion,
            [venta.negocioTelefono, venta.negocioCorreo]
              .filter(Boolean)
              .join(" · "),
          ]
            .filter(Boolean)
            .join("\n")}
        </p>
        <h2>
          Tiquete {venta.prefijo}
          {venta.numero}
        </h2>
        {venta.estado === "Anulada" && (
          <p className="anulada">ANULADA — {venta.motivoAnulacion}</p>
        )}
        <p>
          {fecha(venta.fecha)}
          <br />
          Cajero: {venta.nombreCajero}
          <br />
          {nombreOrden(venta.tipoOrden)}
        </p>
        {venta.lineas.map((l, i) => (
          <div className="linea-tiquete" key={l.id || i}>
            <div className="fila">
              <strong>
                {l.cantidad} × {l.nombre}
              </strong>
              <strong>{moneda(l.total)}</strong>
            </div>
            <p>
              Unitario: {moneda(l.precioUnitario)}
              {l.nota && (
                <>
                  <br />
                  Nota: {l.nota}
                </>
              )}
            </p>
            {l.descuentoAutomatico > 0 && (
              <p>Descuento automático: −{moneda(l.descuentoAutomatico)}</p>
            )}
            <DetalleElecciones linea={l} />
            {l.descuentoManual > 0 && (
              <p>
                Descuento manual: −{moneda(l.descuentoManual)} (
                {l.motivoDescuento})
              </p>
            )}
            {l.descuentoGlobal > 0 && (
              <p>Parte de descuento global: −{moneda(l.descuentoGlobal)}</p>
            )}
            <p>
              Base: {moneda(l.base)} · IVA {l.tarifaIva}%: {moneda(l.impuesto)}
            </p>
          </div>
        ))}
        <dl>
          <dt>Subtotal</dt>
          <dd>{moneda(venta.subtotal)}</dd>
          <dt>Descuentos</dt>
          <dd>−{moneda(venta.descuento)}</dd>
          <dt>Base</dt>
          <dd>{moneda(venta.base)}</dd>
          <dt>IVA</dt>
          <dd>{moneda(venta.impuesto)}</dd>
          <dt>
            <strong>Total CRC</strong>
          </dt>
          <dd>
            <strong>{moneda(venta.total)}</strong>
          </dd>
        </dl>
        {venta.motivoDescuentoGlobal && (
          <p>Motivo del descuento global: {venta.motivoDescuentoGlobal}</p>
        )}
        <h3>Pagos</h3>
        {venta.pagos.map((p) => (
          <p key={p.id}>
            {nombreMetodo(p.metodo)}: {moneda(p.monto)}
            {p.referencia && (
              <>
                <br />
                Referencia: {p.referencia}
              </>
            )}
            {p.metodo === "Efectivo" && (
              <>
                <br />
                Recibido: {moneda(p.recibido)} · Vuelto: {moneda(p.vuelto)}
              </>
            )}
          </p>
        ))}
        <p>
          <strong>Vuelto: {moneda(venta.vuelto)}</strong>
        </p>
        <p className="pie-tiquete">{venta.pieTiquete}</p>
      </article>
    </section>
  );
}
function TablaMetodos({
  titulo,
  filas,
}: {
  titulo: string;
  filas: ResumenMetodo[];
}) {
  return (
    <>
      <h2>{titulo}</h2>
      <table>
        <thead>
          <tr>
            <th>Método</th>
            <th>Ventas con este método</th>
            <th>Monto aplicado</th>
          </tr>
        </thead>
        <tbody>
          {filas.map((f) => (
            <tr key={f.metodo}>
              <td>{nombreMetodo(f.metodo)}</td>
              <td>{f.cantidad}</td>
              <td>{moneda(f.monto)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
export function ReporteSesion({
  reporte: r,
  volver,
}: {
  reporte: Reporte;
  volver: () => void;
}) {
  return (
    <section>
      <div className="acciones no-imprimir">
        <button className="primario" onClick={() => window.print()}>
          Imprimir reporte / PDF
        </button>
        <button onClick={volver}>Volver</button>
      </div>
      <article className="imprimible reporte-carta">
        <h1>Pollotikito — Reporte de sesión</h1>
        <p>
          Cajero: {r.sesion.nombreCajero}
          <br />
          Apertura: {fecha(r.sesion.abiertaEn)}
          <br />
          Cierre:{" "}
          {r.sesion.cerradaEn
            ? fecha(r.sesion.cerradaEn)
            : "Sesión abierta — reporte provisional"}
          <br />
          Sesión: {r.sesion.id}
        </p>
        {r.sesion.cerradaSinConteo && (
          <p className="anulada">
            CERRADA SIN CONTEO — no se registraron contado ni diferencia.
          </p>
        )}
        {r.sesion.motivoCierreForzado && (
          <p>Cierre forzado: {r.sesion.motivoCierreForzado}</p>
        )}
        <dl>
          <dt>Monto inicial</dt>
          <dd>{moneda(r.sesion.montoInicial)}</dd>
          <dt>Ventas vigentes ({r.cantidadVentas})</dt>
          <dd>{moneda(r.totalVentas)}</dd>
          <dt>Descuentos en ventas vigentes</dt>
          <dd>{moneda(r.totalDescuentos)}</dd>
          <dt>Depósitos</dt>
          <dd>{moneda(r.depositos)}</dd>
          <dt>Retiros</dt>
          <dd>{moneda(r.retiros)}</dd>
          <dt>Efectivo esperado</dt>
          <dd>{moneda(r.esperado)}</dd>
          <dt>Contado</dt>
          <dd>
            {r.sesion.cerradaSinConteo
              ? "Sin conteo"
              : r.contado === null
                ? "Pendiente"
                : moneda(r.contado)}
          </dd>
          <dt>Diferencia (contado − esperado)</dt>
          <dd>
            {r.sesion.cerradaSinConteo
              ? "No disponible"
              : r.diferencia === null
                ? "Pendiente"
                : moneda(r.diferencia)}
          </dd>
        </dl>
        <TablaMetodos
          titulo="Ventas vigentes por método de pago"
          filas={r.ventasPorMetodo}
        />
        <p>
          Una venta mixta figura en cada método utilizado; el monto es solo la
          parte aplicada a ese método.
        </p>
        <h2>
          Anulaciones: {r.cantidadAnulaciones} · {moneda(r.totalAnulaciones)}
        </h2>
        <TablaMetodos
          titulo="Anulaciones por método de pago"
          filas={r.anulacionesPorMetodo}
        />
        {r.anulacionesPorMetodo.some(
          (x) => x.metodo !== "Efectivo" && x.cantidad > 0,
        ) && (
          <p>
            <strong>
              Recordá reversar el cobro en el datáfono o devolver el SINPE. La
              anulación en Pollotikito no revierte pagos externos.
            </strong>
          </p>
        )}
        <h2>Movimientos</h2>
        {r.detalleVentas && (
          <>
            <h2>Detalle de ventas y elecciones</h2>
            {r.detalleVentas.map((v) => (
              <div className="linea-tiquete" key={v.id}>
                <h3>
                  {v.prefijo}
                  {v.numero} · {v.estado} · {moneda(v.total)}
                </h3>
                {v.lineas.map((l) => (
                  <div key={l.id}>
                    <p>
                      {l.cantidad} × {l.nombre} · {moneda(l.total)}
                    </p>
                    <DetalleElecciones linea={l} />
                    {l.nota && <p>Nota: {l.nota}</p>}
                  </div>
                ))}
              </div>
            ))}
          </>
        )}
        <table>
          <thead>
            <tr>
              <th>Fecha</th>
              <th>Tipo</th>
              <th>Motivo</th>
              <th>Monto</th>
            </tr>
          </thead>
          <tbody>
            {r.movimientos.map((m) => (
              <tr key={m.id}>
                <td>{fecha(m.fecha)}</td>
                <td>{m.tipo === "Deposito" ? "Depósito" : "Retiro"}</td>
                <td>{m.motivo}</td>
                <td>{moneda(m.monto)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {r.sesion.conteo.length > 0 && (
          <>
            <h2>Conteo por denominación</h2>
            <table>
              <thead>
                <tr>
                  <th>Denominación</th>
                  <th>Cantidad</th>
                  <th>Subtotal</th>
                </tr>
              </thead>
              <tbody>
                {r.sesion.conteo.map((c) => (
                  <tr key={c.denominacion}>
                    <td>{moneda(c.denominacion)}</td>
                    <td>{c.cantidad}</td>
                    <td>{moneda(c.denominacion * c.cantidad)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </article>
    </section>
  );
}
export function CierreForm({
  sesion,
  forzado,
  terminado,
  cancelar,
}: {
  sesion: Sesion;
  forzado: boolean;
  terminado: (r: Reporte) => void;
  cancelar: () => void;
}) {
  const [cantidades, setCantidades] = useState<Record<number, number>>({}),
    [sinConteo, setSinConteo] = useState(false),
    [motivo, setMotivo] = useState(""),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  const total = denominaciones.reduce(
    (a, d) => a + d * (cantidades[d] || 0),
    0,
  );
  return (
    <form
      className="panel"
      onSubmit={(e) => {
        e.preventDefault();
        if (
          !confirm(
            "¿Cerrar la sesión? Después del cierre no se podrá modificar.",
          )
        )
          return;
        setBusy(true);
        setError("");
        api<Reporte>(
          `/caja/${sesion.id}/${forzado ? "forzar-cierre" : "cerrar"}`,
          "POST",
          {
            conteo: sinConteo
              ? null
              : denominaciones.map((d) => ({
                  denominacion: d,
                  cantidad: cantidades[d] || 0,
                })),
            motivo: forzado ? motivo : null,
          },
        )
          .then(terminado)
          .catch((e) => setError(e.message))
          .finally(() => setBusy(false));
      }}
    >
      <h2>
        {forzado ? "Forzar cierre de" : "Cerrar"} caja — {sesion.nombreCajero}
      </h2>
      <fieldset disabled={busy}>
        {forzado && (
          <>
            <Campo titulo="Motivo obligatorio">
              <input
                value={motivo}
                onChange={(e) => setMotivo(e.target.value)}
                maxLength={500}
                required
              />
            </Campo>
            <label className="check">
              <input
                type="checkbox"
                checked={sinConteo}
                onChange={(e) => setSinConteo(e.target.checked)}
              />
              Cerrar sin conteo
            </label>
          </>
        )}
        {!sinConteo ? (
          <>
            <div className="conteo-grid">
              {denominaciones.map((d) => (
                <Campo key={d} titulo={moneda(d)}>
                  <input
                    aria-label={`Cantidad de ${d}`}
                    type="number"
                    min={0}
                    max={1000000}
                    step={1}
                    value={cantidades[d] || 0}
                    onChange={(e) =>
                      setCantidades({
                        ...cantidades,
                        [d]: Number(e.target.value),
                      })
                    }
                    required
                  />
                </Campo>
              ))}
            </div>
            <p className="total">Contado: {moneda(total)}</p>
          </>
        ) : (
          <p>
            La sesión quedará cerrada sin conteo. El contado y la diferencia no
            estarán disponibles.
          </p>
        )}
        <ErrorTexto texto={error} />
        <div className="acciones">
          <button className="primario">Confirmar cierre</button>
          <button type="button" onClick={cancelar}>
            Cancelar
          </button>
        </div>
      </fieldset>
    </form>
  );
}
export function VentasTurno({
  sesionId,
  anulable,
  ver,
}: {
  sesionId: string;
  anulable: boolean;
  ver: (v: Venta) => void;
}) {
  const [ventas, setVentas] = useState<Venta[]>([]),
    [numero, setNumero] = useState(""),
    [error, setError] = useState(""),
    [aviso, setAviso] = useState(""),
    [anular, setAnular] = useState<Venta | null>(null),
    [motivo, setMotivo] = useState(""),
    [busy, setBusy] = useState(false);
  async function cargar() {
    setVentas(
      await api<Venta[]>(
        `/caja/${sesionId}/ventas?numero=${encodeURIComponent(numero)}`,
      ),
    );
  }
  useEffect(() => {
    cargar().catch((e) => setError(e.message));
  }, [sesionId]);
  return (
    <div className="panel">
      <h2>Ventas de la sesión</h2>
      <form
        className="acciones"
        onSubmit={(e) => {
          e.preventDefault();
          cargar().catch((e) => setError(e.message));
        }}
      >
        <Campo titulo="Buscar por número">
          <input value={numero} onChange={(e) => setNumero(e.target.value)} />
        </Campo>
        <button>Buscar</button>
      </form>
      <ErrorTexto texto={error} />
      {aviso && (
        <p role="status" className="aviso">
          {aviso}
        </p>
      )}
      {anular && (
        <form
          className="panel"
          onSubmit={(e) => {
            e.preventDefault();
            setBusy(true);
            api<{ aviso: string | null }>(
              `/ventas/${anular.id}/anular`,
              "POST",
              { motivo },
            )
              .then(async (r) => {
                setAviso(r.aviso || "Venta anulada.");
                setAnular(null);
                setMotivo("");
                await cargar();
              })
              .catch((e) => setError(e.message))
              .finally(() => setBusy(false));
          }}
        >
          <h3>
            Anular {anular.prefijo}
            {anular.numero}
          </h3>
          {anular.pagos.some((p) => p.metodo !== "Efectivo") && (
            <p className="aviso">
              Recordá reversar el cobro en el datáfono o devolver el SINPE
            </p>
          )}
          <fieldset disabled={busy}>
            <Campo titulo="Motivo obligatorio">
              <input
                value={motivo}
                onChange={(e) => setMotivo(e.target.value)}
                maxLength={500}
                required
              />
            </Campo>
            <div className="acciones">
              <button className="primario">Confirmar anulación</button>
              <button type="button" onClick={() => setAnular(null)}>
                Cancelar
              </button>
            </div>
          </fieldset>
        </form>
      )}
      <div className="tabla">
        <table>
          <thead>
            <tr>
              <th>Número</th>
              <th>Fecha</th>
              <th>Total</th>
              <th>Estado</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {ventas.map((v) => (
              <tr key={v.id}>
                <td>
                  {v.prefijo}
                  {v.numero}
                </td>
                <td>{fecha(v.fecha)}</td>
                <td>{moneda(v.total)}</td>
                <td>{v.estado}</td>
                <td>
                  <div className="acciones">
                    <button onClick={() => ver(v)}>Ver / reimprimir</button>
                    {anulable && v.estado === "Pagada" && (
                      <button
                        onClick={() => {
                          setAnular(v);
                          setMotivo("");
                        }}
                      >
                        Anular
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
export function SesionesAdmin() {
  const [sesiones, setSesiones] = useState<Sesion[]>([]),
    [cerrar, setCerrar] = useState<Sesion | null>(null),
    [reporte, setReporte] = useState<Reporte | null>(null),
    [ventas, setVentas] = useState<string | null>(null),
    [tiquete, setTiquete] = useState<Venta | null>(null),
    [error, setError] = useState("");
  async function cargar() {
    setSesiones(await api<Sesion[]>("/caja/sesiones"));
  }
  useEffect(() => {
    cargar().catch((e) => setError(e.message));
  }, []);
  if (tiquete)
    return <Tiquete venta={tiquete} volver={() => setTiquete(null)} />;
  if (reporte)
    return <ReporteSesion reporte={reporte} volver={() => setReporte(null)} />;
  return (
    <section>
      <h1>Sesiones de caja</h1>
      <ErrorTexto texto={error} />
      <button onClick={() => void cargar().catch((e) => setError(e.message))}>
        Actualizar
      </button>
      {cerrar && (
        <CierreForm
          sesion={cerrar}
          forzado
          terminado={(r) => {
            setCerrar(null);
            setReporte(r);
            void cargar();
          }}
          cancelar={() => setCerrar(null)}
        />
      )}
      <div className="tabla">
        <table>
          <thead>
            <tr>
              <th>Cajero</th>
              <th>Apertura</th>
              <th>Estado</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {sesiones.map((s) => (
              <tr key={s.id}>
                <td>{s.nombreCajero}</td>
                <td>{fecha(s.abiertaEn)}</td>
                <td>
                  {s.cerradaSinConteo
                    ? "Cerrada sin conteo"
                    : s.cerradaEn
                      ? "Cerrada"
                      : "Abierta"}
                </td>
                <td>
                  <div className="acciones">
                    <button
                      onClick={() => {
                        api<Reporte>(`/caja/${s.id}/reporte`)
                          .then(setReporte)
                          .catch((e) => setError(e.message));
                      }}
                    >
                      Reporte
                    </button>
                    <button onClick={() => setVentas(s.id)}>Ventas</button>
                    {!s.cerradaEn && (
                      <button onClick={() => setCerrar(s)}>
                        Forzar cierre
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {ventas && (
        <VentasTurno sesionId={ventas} anulable={false} ver={setTiquete} />
      )}
    </section>
  );
}
