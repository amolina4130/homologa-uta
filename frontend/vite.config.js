import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
// En desarrollo, /api se reenvía al gateway. Se usa [::1] porque en algunos equipos
// 127.0.0.1:8080 lo ocupa otro programa (Oracle); Docker publica el gateway también en IPv6.
const GATEWAY = globalThis.process?.env?.GATEWAY_URL || 'http://[::1]:8080'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: GATEWAY,
        changeOrigin: true,
        rewrite: (ruta) => ruta.replace(/^\/api/, ''),
      },
    },
  },
})
