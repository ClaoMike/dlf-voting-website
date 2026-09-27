import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // The app calls the API at /api on its own address, as in production (where the backend serves the site).
    // Locally, Vite passes those calls on to the backend.
    // API_PROXY_TARGET points it at a backend on another port if 5120 is taken.
    proxy: {
      '/api': process.env.API_PROXY_TARGET ?? 'http://localhost:5120',
    },
  },
})
