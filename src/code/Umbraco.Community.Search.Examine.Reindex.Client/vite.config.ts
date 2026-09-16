import { defineConfig } from "vite";

export default defineConfig({
    build: {
        lib: {
            entry: "src/entry.ts", // your web component source file
            formats: ["es"],
            fileName: "entry-point", // the name of the built file
        },
        outDir: "../Umbraco.Community.Search.Examine.Reindex/wwwroot/App_Plugins/searchreindex", // all compiled files will be placed here
        emptyOutDir: true,
        sourcemap: true,
        rollupOptions: {
            external: [/^@umbraco/], // ignore the Umbraco Backoffice package in the build
        },
    }   
});