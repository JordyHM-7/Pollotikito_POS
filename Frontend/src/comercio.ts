import type { EleccionComponente } from "./menu";
export type TipoDescuento = "Ninguno" | "Porcentaje" | "Monto";
export type TipoOrden = "ComerAqui" | "ParaLlevar" | "Express";
export type MetodoPago = "Efectivo" | "Tarjeta" | "Sinpe";
export type Categoria = {
  id: string;
  nombre: string;
  orden: number;
  activo: boolean;
};
export type Producto = {
  esCombo: boolean;
  disponible: boolean;
  advertencias: string[];
  id: string;
  categoriaId: string;
  nombre: string;
  precio: number;
  tarifaIva: number;
  foto: string | null;
  orden: number;
  activo: boolean;
  tipoDescuento: TipoDescuento;
  valorDescuento: number;
  descuentoDesde: string | null;
  descuentoHasta: string | null;
};
export type Descuento = {
  tipo: TipoDescuento;
  valor: number;
  motivo: string | null;
};
export const sinDescuento: Descuento = {
  tipo: "Ninguno",
  valor: 0,
  motivo: null,
};
export type LineaSolicitud = {
  modificadores?: string[];
  componentes?: EleccionComponente[];
  productoId: string;
  cantidad: number;
  nota: string;
  descuento: Descuento;
};
export type PagoSolicitud = {
  metodo: MetodoPago;
  monto: number;
  recibido: number | null;
  referencia: string | null;
};
export type SolicitudVenta = {
  id: string;
  sesionCajaId: string;
  tipoOrden: TipoOrden;
  lineas: LineaSolicitud[];
  pagos: PagoSolicitud[];
  descuento: Descuento;
};
export type Sesion = {
  id: string;
  cajeroId: string;
  nombreCajero: string;
  abiertaEn: string;
  montoInicial: number;
  cerradaEn: string | null;
  cerradaPorId: string | null;
  motivoCierreForzado: string | null;
  cerradaSinConteo: boolean;
  esperado: number | null;
  contado: number | null;
  diferencia: number | null;
  conteo: { denominacion: number; cantidad: number }[];
};
export type Linea = {
  modificadores?: {
    nombreGrupo: string;
    nombreOpcion: string;
    precioAdicional: number;
  }[];
  componentes?: {
    nombreComponente: string;
    nombreProducto: string;
    cantidadPorUnidad: number;
    recargoUnitario: number;
  }[];
  id: string;
  nombre: string;
  cantidad: number;
  nota: string;
  precioUnitario: number;
  tarifaIva: number;
  descuentoAutomatico: number;
  descuentoManual: number;
  descuentoGlobal: number;
  motivoDescuento: string | null;
  base: number;
  impuesto: number;
  total: number;
};
export type Pago = PagoSolicitud & { id: string; vuelto: number };
export type Venta = {
  id: string;
  sesionCajaId: string;
  nombreCajero: string;
  prefijo: string;
  numero: number;
  tipoOrden: TipoOrden;
  estado: "Pagada" | "Anulada";
  fecha: string;
  subtotal: number;
  descuento: number;
  base: number;
  impuesto: number;
  total: number;
  vuelto: number;
  motivoDescuentoGlobal: string | null;
  motivoAnulacion: string | null;
  anuladaEn: string | null;
  negocioNombre: string;
  negocioRazonSocial: string;
  negocioCedula: string;
  negocioTelefono: string;
  negocioCorreo: string;
  negocioDireccion: string;
  tieneLogo: boolean;
  pieTiquete: string;
  lineas: Linea[];
  pagos: Pago[];
};
export type ResumenMetodo = {
  metodo: MetodoPago;
  cantidad: number;
  monto: number;
};
export type Movimiento = {
  id: string;
  tipo: "Deposito" | "Retiro";
  monto: number;
  motivo: string;
  fecha: string;
};
export type Reporte = {
  detalleVentas?: Venta[];
  sesion: Sesion;
  ventasPorMetodo: ResumenMetodo[];
  anulacionesPorMetodo: ResumenMetodo[];
  cantidadVentas: number;
  totalVentas: number;
  cantidadAnulaciones: number;
  totalAnulaciones: number;
  totalDescuentos: number;
  depositos: number;
  retiros: number;
  esperado: number;
  contado: number | null;
  diferencia: number | null;
  movimientos: Movimiento[];
};
export const denominaciones = [
  5, 10, 25, 50, 100, 500, 1000, 2000, 5000, 10000, 20000,
];
export const metodos: MetodoPago[] = ["Efectivo", "Tarjeta", "Sinpe"];
export const nombreMetodo = (m: MetodoPago) =>
  m === "Sinpe" ? "SINPE Móvil" : m;
export const nombreOrden = (t: TipoOrden) =>
  ({ ComerAqui: "Comer aquí", ParaLlevar: "Para llevar", Express: "Express" })[
    t
  ];
export const moneda = (v: number | null) =>
  v === null
    ? "Sin conteo"
    : "₡" +
      new Intl.NumberFormat("fr-FR", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      })
        .format(v)
        .replace(/[\u00a0\u202f]/g, " ");
export const fecha = (v: string) =>
  new Date(v).toLocaleString("es-CR", { timeZone: "America/Costa_Rica" });
export const centavos = (n: number) =>
  Math.round((n + Number.EPSILON) * 100) / 100;
export function uuidV7() {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  let ms = BigInt(Date.now());
  for (let i = 5; i >= 0; i--) {
    bytes[i] = Number(ms & 255n);
    ms >>= 8n;
  }
  bytes[6] = (bytes[6] & 15) | 112;
  bytes[8] = (bytes[8] & 63) | 128;
  const h = Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}
