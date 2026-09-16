import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { resolve } from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react()
  ],
  server: {
    port: 5273,
    proxy: {
      '/api': {
        target: 'http://localhost:9180',
        changeOrigin: true,
        rewrite: (path) => `/workbenchproxy${path}`,
      },
      // The APISIX OIDC callback lands here so its session cookie is issued
      // for the local Vite origin, not for the gateway's port directly.
      '/workbenchproxy': {
        target: 'http://localhost:9180',
        changeOrigin: true,
      },
    },
  },
  css: {
    preprocessorOptions: {
      less: {
        javascriptEnabled: true
      }
    },
  },
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
      '~': resolve(__dirname, './'),
    }
  },
  build: {
    manifest: true,
    rollupOptions: {
      output: {
        sourcemap: false
      }
    }
  }
})
