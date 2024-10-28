import { defineConfig } from "vite";

export default defineConfig(({ command }) => {

  const buildMode = process.env.build_mode?.trim();

  return {
    build: {
      lib: {
        entry: "src/entry.ts",
        formats: ["es"],
        name : "HCS.Media.AccessibleMediaPicker",
      },
      outDir: "../wwwroot/App_Plugins/HCS.Media.AccessibleMediaPicker",
      sourcemap: buildMode == 'development',
      rollupOptions: {
        external: [/^@umbraco/]
      },
    }
  }
});
