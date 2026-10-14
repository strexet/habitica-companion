// Remote endpoints are inert placeholders until an operator configures deployment.
const resources = {
  local: { pwaOrigin: "http://localhost:5081", issuer: "http://localhost:8787", resource: "http://localhost:8788/mcp" },
  preview: { pwaOrigin: "https://habitica-companion-preview.example.invalid", issuer: "https://habitica-auth-preview.example.invalid", resource: "https://habitica-mcp-preview.example.invalid/mcp" },
  production: { pwaOrigin: "https://habitica-companion.pages.dev", issuer: "https://habitica-auth.example.invalid", resource: "https://habitica-mcp.example.invalid/mcp" },
} as const;

export function resourceConfiguration(environment: string) {
  if (environment !== "local" && environment !== "preview" && environment !== "production") {
    throw new Error("Unknown integration environment.");
  }
  return resources[environment];
}
