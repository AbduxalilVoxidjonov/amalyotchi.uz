/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const API_TARGET = process.env['VITE_DEV_API_TARGET'] ?? 'http://127.0.0.10:5080';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    // Amaliyotchi uchun ajratilgan loopback IP (macOS: `sudo ifconfig lo0 alias 127.0.0.10 up`).
    host: '127.0.0.10',
    port: 5174,
    proxy: {
      '/api': { target: API_TARGET, changeOrigin: true },
    },
  },
  build: {
    // TWA — mobil, sekin tarmoq: bundle kichik bo'lsin. Og'ir kutubxona qo'shilmaydi.
    target: 'es2020',
    sourcemap: false,
    cssCodeSplit: true,
    // react-dom o'zi ~220 kB (69 kB gzip) — bu chegara faqat ILOVA kodi/vendorlar o'sishini ushlab turadi.
    chunkSizeWarningLimit: 250,
    rollupOptions: {
      output: {
        // Vendor'lar alohida — ilova kodi o'zgarganda foydalanuvchi keshi saqlanadi.
        manualChunks(id) {
          if (!id.includes('/node_modules/')) return undefined;
          if (id.includes('/@twa-dev/')) return 'telegram';
          if (id.includes('/react-router')) return 'router';
          if (id.includes('/@tanstack/')) return 'query';
          if (id.includes('/react/') || id.includes('/react-dom/') || id.includes('/scheduler/'))
            return 'react';
          return 'vendor';
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
