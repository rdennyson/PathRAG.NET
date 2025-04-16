import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  server: {
    port: 3001,
  },
  plugins: [react()],
  build: {
    chunkSizeWarningLimit: 2200,
    outDir: "../Backend/PathRAG.Api/wwwroot",
    target: "esnext",
    sourcemap: true,
  },
  optimizeDeps: {
    exclude: ['mammoth']
  }
})
