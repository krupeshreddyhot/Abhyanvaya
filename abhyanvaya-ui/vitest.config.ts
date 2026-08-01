import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    // AI22.7B a11y checker tests need a minimal DOM; other suites remain environment-agnostic.
    environment: "happy-dom",
    include: ["src/**/*.test.ts"],
  },
});
