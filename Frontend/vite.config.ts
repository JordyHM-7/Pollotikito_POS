import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": {
        target: "http://localhost:5149",
        changeOrigin: true,
        configure(proxy) {
          proxy.on("error", (_error, _request, response) => {
            if (!("writeHead" in response) || response.headersSent) return;
            response.writeHead(503, {
              "Content-Type": "application/problem+json; charset=utf-8",
              "Cache-Control": "no-store",
            });
            response.end(
              JSON.stringify({
                status: 503,
                title:
                  "La API no está disponible. Iniciá el backend de Pollotikito y volvé a intentar.",
              }),
            );
          });
        },
      },
    },
  },
});
