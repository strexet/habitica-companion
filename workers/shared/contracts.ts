export interface AuthProps { profileId: string; connectionId: string; grantEpoch?:string }
export function authProps(value: unknown): AuthProps | null {
  if (!value || typeof value !== "object") return null;
  const record = value as Record<string, unknown>;
  if (typeof record.profileId !== "string" || !/^prf_[A-Za-z0-9_-]{43}$/.test(record.profileId) || typeof record.connectionId !== "string" || !record.connectionId) return null;
  return { profileId: record.profileId, connectionId: record.connectionId, ...(typeof record.grantEpoch==='string'?{grantEpoch:record.grantEpoch}:{}) };
}
