import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

// https://vite.dev/config/
export default defineConfig({
  plugins: [svelte()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:19421',
        changeOrigin: true,
      },
      '/ws': {
        target: 'ws://localhost:19421',
        ws: true,
        changeOrigin: true,
      },
    },
  },
})
