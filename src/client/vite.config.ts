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
  server: {
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
