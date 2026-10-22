import type { Producto } from "./comercio";
export type Opcion = {
  id: string;
  nombre: string;
  precioAdicional: number;
  orden: number;
  activo: boolean;
};
export type Grupo = {
  id: string;
  nombre: string;
  minimo: number;
  maximo: number;
  orden: number;
  activo: boolean;
  opciones: Opcion[];
};
export type Asignacion = {
  grupoModificadorId: string;
  productoId?: string;
  orden: number;
  activo?: boolean;
};
export type Alternativa = {
  productoId: string;
  recargo: number;
  orden: number;
  activo?: boolean;
};
export type Componente = {
  id?: string;
  productoId?: string;
  nombre: string;
  tipo: "Fijo" | "Elegible";
  cantidad: number;
  productoFijoId: string | null;
  orden: number;
  activo?: boolean;
  opciones: Alternativa[];
};
export type EleccionComponente = {
  componenteId: string;
  productoId: string;
  cantidad: number;
};
export type Menu = {
  grupos: Grupo[];
  asignaciones: Asignacion[];
  componentes: Componente[];
  productosComponentes: Producto[];
};
