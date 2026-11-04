export const recoveryVersion = 'integration-recovery-v1';
export function recoveryDescriptor(profile: {profile_id:string; connection_id:string}) {
  // This reference is intentionally not a bearer credential or proof of consent.
  return {version:recoveryVersion, profileId:profile.profile_id, connectionId:profile.connection_id};
}
