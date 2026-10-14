export const readScopes = { get_profile: "profile:read", get_account_status: "account:read", list_tasks: "tasks:read", get_task: "tasks:read" } as const;
export type ReadTool = keyof typeof readScopes;
export type ReadInput = { type?: "habit" | "daily" | "todo" | "reward"; completed?: boolean; dueOnly?: boolean; query?: string; limit?: number; taskId?: string };
export function readInput(tool: string, value: unknown): ReadInput {
  if (!(tool in readScopes) || !Object.hasOwn(readScopes, tool) || !value || typeof value !== "object" || Array.isArray(value)) throw new Error("Invalid tool input.");
  const input = value as Record<string, unknown>;
  const allowed = tool === "list_tasks" ? ["type", "completed", "dueOnly", "query", "limit"] : tool === "get_task" ? ["taskId"] : [];
  if (Object.keys(input).some(key => !allowed.includes(key))) throw new Error("Invalid tool input.");
  if (input.type !== undefined && !["habit", "daily", "todo", "reward"].includes(input.type as string)) throw new Error("Invalid task type.");
  for (const key of ["completed", "dueOnly"]) if (input[key] !== undefined && typeof input[key] !== "boolean") throw new Error("Invalid filter.");
  if (input.query !== undefined && (typeof input.query !== "string" || input.query.length > 256)) throw new Error("Invalid query.");
  if (input.limit !== undefined && (!Number.isInteger(input.limit) || Number(input.limit) < 1 || Number(input.limit) > 100)) throw new Error("Invalid limit.");
  if (tool === "get_task" && (typeof input.taskId !== "string" || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(input.taskId))) throw new Error("Invalid task ID.");
  if (input.dueOnly && input.type && input.type !== "daily") throw new Error("Due filter requires Dailies.");
  return input as ReadInput;
}
