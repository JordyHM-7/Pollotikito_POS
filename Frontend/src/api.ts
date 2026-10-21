export type Usuario = {
  id: string;
  nombre: string;
  nombreUsuario: string;
  rol: "Administrador" | "Cajero";
  activo: boolean;
  tienePin: boolean;
  debeCambiarCredenciales: boolean;
};
export type Configuracion = {
  nombreComercial: string;
  razonSocial: string;
  cedula: string;
  telefono: string;
  correo: string;
  direccion: string;
  pieTiquete: string;
  ivaPorDefecto: number;
  logo: string | null;
};
export type Consecutivo = {
  tipo: string;
  prefijo: string;
  siguienteNumero: number;
};
let csrf = "";
export async function renovarCsrf() {
  csrf = (await api<{ token: string }>("/auth/csrf")).token;
}
export async function api<T>(
  ruta: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  if (method !== "GET" && !csrf) await renovarCsrf();
  const binario = body instanceof File;
  const r = await fetch("/api" + ruta, {
    method,
    credentials: "same-origin",
    headers:
      method === "GET"
        ? {}
        : {
            "X-CSRF-TOKEN": csrf,
            "Content-Type": binario
              ? "application/octet-stream"
              : "application/json",
          },
    body:
      body === undefined ? undefined : binario ? body : JSON.stringify(body),
  });
  if (!r.ok) {
    const p = await r.json().catch(() => ({}));
    if (
      r.status === 401 &&
      ruta !== "/auth/me" &&
      !ruta.startsWith("/auth/login")
    )
      window.dispatchEvent(new Event("sesion-vencida"));
    throw new Error(p.title || `Error ${r.status}`);
  }
  return r.status === 204 ? (undefined as T) : r.json();
}
