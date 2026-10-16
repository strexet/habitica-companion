// Remote endpoints are inert placeholders until an operator configures deployment.
const resources = {
  local: { workspaceOrigin: "http://localhost:8789", pwaOrigin: "http://localhost:5081", issuer: "http://localhost:8787", resource: "http://localhost:8788/mcp" },
  preview: { workspaceOrigin: "https://companion-workspace-preview.example.invalid", pwaOrigin: "https://habitica-companion-preview.example.invalid", issuer: "https://habitica-auth-preview.example.invalid", resource: "https://habitica-mcp-preview.example.invalid/mcp" },
  production: { workspaceOrigin: "https://companion-workspace.example.invalid", pwaOrigin: "https://habitica-companion.pages.dev", issuer: "https://habitica-auth.example.invalid", resource: "https://habitica-mcp.example.invalid/mcp" },
} as const;

export function resourceConfiguration(environment: string, bindings?: unknown) {
  if (environment !== "local" && environment !== "preview" && environment !== "production") throw new Error("Unknown integration environment.");
  const source = bindings && typeof bindings === "object" ? bindings as Record<string,unknown> : {};
  const keys=["PWA_ORIGIN","AUTH_ISSUER","MCP_RESOURCE","WORKSPACE_ORIGIN"];
  if(keys.every(key=>source[key]===undefined))return resources[environment];
  if(keys.some(key=>typeof source[key]!=="string"))throw new Error("Incomplete integration origins.");
  const values=keys.map(key=>source[key] as string);
  const urls=values.map(value=>new URL(value));
  if(urls.some((url,index)=>url.protocol!=="https:"||url.username||url.password||url.search||url.hash||url.hostname.endsWith(".invalid")||url.href!==(index===2?url.origin+"/mcp":url.origin+"/")||values[index]!==(index===2?url.origin+"/mcp":url.origin)))throw new Error("Invalid integration origins.");
  if(new Set(urls.map(url=>url.origin)).size!==4)throw new Error("Integration origins must be distinct.");
  return {pwaOrigin:values[0],issuer:values[1],resource:values[2],workspaceOrigin:values[3]};
}
