import type { ReactNode } from "react";
export function Campo({
  titulo,
  children,
}: {
  titulo: string;
  children: ReactNode;
}) {
  return (
    <label>
      {titulo}
      {children}
    </label>
  );
}
export function ErrorTexto({ texto }: { texto: string }) {
  return texto ? (
    <p role="alert" className="error">
      {texto}
    </p>
  ) : null;
}
