import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: 'swagger.json',
  output: 'src/api',
  plugins: [
    {
      name: '@hey-api/client-fetch',
      runtimeConfigPath: '../hey-api.ts',
    },
    '@hey-api/sdk',
  ],
});
