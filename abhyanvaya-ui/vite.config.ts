import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // Proxy target for local media/API. Prefer VITE_API_PROXY_TARGET when set.
  // Default matches launch profile "https" HTTP URL (Properties/launchSettings.json).
  // If you start the API without a profile (often :5000), set VITE_API_PROXY_TARGET accordingly.
  const apiProxyTarget = env.VITE_API_PROXY_TARGET || 'http://localhost:5210'

  return {
    plugins: [react()],
    server: {
      proxy: {
        '/media': {
          target: apiProxyTarget,
          changeOrigin: true,
          secure: false,
        },
        '/api': {
          target: apiProxyTarget,
          changeOrigin: true,
          secure: false,
        },
        '/hubs': {
          target: apiProxyTarget,
          changeOrigin: true,
          secure: false,
          ws: true,
        },
      },
    },
  }
})
