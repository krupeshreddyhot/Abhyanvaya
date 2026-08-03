import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // Proxy target for local media/API. Prefer VITE_API_PROXY_TARGET when set.
  // Default http://localhost:5000 matches the common local Kestrel bind used when the API
  // is started without launchSettings (Visual Studio / direct exe). Profile "https" uses 5210/7063.
  const apiProxyTarget = env.VITE_API_PROXY_TARGET || 'http://localhost:5000'

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
