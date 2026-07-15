/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'
import path from 'path'

// Plugin to suppress Vite client script errors when HMR is unreliable
const suppressViteClientErrors = () => ({
  name: 'suppress-vite-client-errors',
  transformIndexHtml: {
    order: 'post',
    handler(html: string) {
      return html.replace(
        /<script type="module" src="\/\@vite\/client"><\/script>/,
        '<script type="module">/* vite client suppressed */</script>'
      );
    }
  }
});

// https://vite.dev/config/
export default defineConfig({
  plugins: [svelte(), suppressViteClientErrors()],
  resolve: {
    alias: {
      $features: path.resolve(__dirname, './src/features'),
      $shared: path.resolve(__dirname, './src/shared'),
      $renderer: path.resolve(__dirname, './src/renderer'),
      $config: path.resolve(__dirname, './src/config'),
      $app: path.resolve(__dirname, './src/app'),
    }
  },
  build: {
    rollupOptions: {
      output: {
        // Split heavy third-party code out of the app entry so the initial
        // index chunk stays small and vendor code (mostly three.js) caches
        // independently of frequent app changes.
        manualChunks(id: string) {
          if (!id.includes('node_modules')) return;
          if (id.includes('three') || id.includes('@dimforge') || id.includes('@tweenjs')) {
            return 'three';
          }
          return 'vendor';
        },
      },
    },
    // three.js is an irreducibly large 3D dependency; its isolated chunk runs
    // ~600 kB. Raise the warning ceiling above it so a genuinely oversized app
    // chunk still trips the warning.
    chunkSizeWarningLimit: 700,
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
  server: {
    port: 8378,
    hmr: false,
    host: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:19421',
        changeOrigin: true,
      },
      '/ws': {
        target: 'http://127.0.0.1:19421',
        ws: true,
        changeOrigin: true,
      },
    },
  },
})
