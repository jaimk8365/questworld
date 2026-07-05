import { defineConfig } from "vite";

export default defineConfig({
  // Hosted deploys set QW_BASE (e.g. "/questworld/" for GitHub Pages).
  base: process.env.QW_BASE || "/",
  server: {
    port: 5173,
    host: true, // expose on the home network for the iPads/iPhone
  },
  build: {
    outDir: "dist",
  },
  clearScreen: false,
});
