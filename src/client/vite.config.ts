import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

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
