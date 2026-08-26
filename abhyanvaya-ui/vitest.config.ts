import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    // AI22.7B a11y + AI29.1D.24B.4A.2 ErrorBoundary recovery need a minimal DOM.
    environment: "happy-dom",
    include: ["src/**/*.test.ts", "src/**/*.test.tsx"],
  },
});
