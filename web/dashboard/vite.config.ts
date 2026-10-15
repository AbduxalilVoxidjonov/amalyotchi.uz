/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const API_TARGET = process.env['VITE_DEV_API_TARGET'] ?? 'http://127.0.0.1:5080';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: false,
    proxy: {
      // Dev'da `/api/*` backend'ga (launchSettings: http://127.0.0.1:5080) yo'naltiriladi —
      // CORS shart emas, VITE_API_URL bo'sh qoladi.
      '/api': {
        target: API_TARGET,
        changeOrigin: true,
      },
    },
  },
  build: {
    sourcemap: true,
    rollupOptions: {
      output: {
        // Funksiya shakli: obyekt shakli faqat paket kirish modulini oladi — `react-dom/client` (asosiy hajm)
        // ilova entry chunk'iga tushib, har deploy'da ~84 kB gzip vendor kodi kesh'dan chiqib ketardi.
        manualChunks(id) {
          if (/[\\/]node_modules[\\/](react|react-dom|react-router|react-router-dom|scheduler)[\\/]/.test(id))
            return 'react';
          if (/[\\/]node_modules[\\/]@tanstack[\\/]/.test(id)) return 'query';
          return undefined;
        },
      },
    },
  },
  test: {
    globals: true,
    // jsdom + Node 22–26 mosligi (Web Storage, File/FormData realm) — shared/src/test/jsdomEnvironment.ts
    environment: '../shared/src/test/jsdomEnvironment.ts',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
  },
});
