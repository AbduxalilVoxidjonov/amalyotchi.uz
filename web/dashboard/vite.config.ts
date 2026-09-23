/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const API_TARGET = process.env['VITE_DEV_API_TARGET'] ?? 'http://127.0.0.10:5080';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    // Amaliyotchi uchun ajratilgan loopback IP (macOS: `sudo ifconfig lo0 alias 127.0.0.10 up`).
    host: '127.0.0.10',
    port: 5173,
    strictPort: false,
    proxy: {
      // Dev'da `/api/*` backend'ga (launchSettings: http://127.0.0.10:5080) yo'naltiriladi —
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
        manualChunks: {
          react: ['react', 'react-dom', 'react-router-dom'],
          query: ['@tanstack/react-query'],
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
