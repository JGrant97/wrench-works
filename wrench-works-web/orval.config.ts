import { defineConfig } from "orval";

export default defineConfig({
  wrenchworks: {
    input: {
      target: "http://localhost:5000/openapi/v1.json",
      override: {
        // See orval.transformer.cjs: .NET 10 emits numbers as ["integer","string"] unions,
        // which Orval would otherwise widen to `number | string` on every numeric field.
        transformer: "./orval.transformer.cjs",
      },
    },
    output: {
      mode: "tags-split",
      target: "src/api/generated",
      schemas: "src/api/generated/models",
      client: "axios-functions",
      httpClient: "axios",
      override: {
        mutator: {
          path: "src/lib/api-client.ts",
          name: "apiClient",
        },
      },
    },
  },
});
