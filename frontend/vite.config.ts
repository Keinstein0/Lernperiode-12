import adapter from '@sveltejs/adapter-auto';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [sveltekit({
    compilerOptions: { runes: ({ filename }) =>
      filename.split(/[/\\]/).includes('node_modules') ? undefined : true },
    adapter: adapter()
  })],
  server: {
    host: '0.0.0.0',
    proxy: { '/api': {
      target: process.env.ROUTER_INTERNAL_URL || 'http://localhost:5000',
      changeOrigin: true,
      rewrite: (path) => path.replace(/^\/api/, '')
    } }
  }
});
