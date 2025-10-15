import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:8080', // ehrbridge_api in Docker
        changeOrigin: true,
        rewrite: (path) => path, // keep /api/audit as-is
      },
    },
  },
})
