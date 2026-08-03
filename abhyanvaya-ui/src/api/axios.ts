import axios from "axios";

// Dev default `/api` goes through the Vite proxy (see vite.config.ts) so browser calls
// share the UI origin and avoid self-signed HTTPS issues on localhost.
const api = axios.create({
  baseURL:
    import.meta.env.VITE_API_BASE_URL ||
    (import.meta.env.DEV ? "/api" : "https://localhost:7063/api"),
  /** Render free-tier cold starts can exceed 30s; avoid failing before Kestrel responds */
  timeout: 120_000,
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem("token");

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

export default api;
