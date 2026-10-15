namespace Habitica.Application.Workspace;

public enum PartyPlanningAction { Context, State, Consent, Settings, DeleteNamespace, ExportOwn, RemoveOwn }
public sealed record PartyPlanningRequest(PartyPlanningAction Action, Guid? NamespaceId = null, long? Revision = null, bool? MembersCanQueue = null, Guid? ExpectedPartyId = null);
public sealed record PartyIdentity(Guid Id, string Name, int? MemberCount);
public sealed record PartyQueueItem(Guid Id, string QuestKey, string ContributorLabel, bool OwnedByYou, DateTimeOffset CreatedAtUtc, int VoteCount, bool VotedByYou);
public sealed record PartyPlanningView(PartyIdentity Party, string MemberRole, DateTimeOffset RetrievedAtUtc, string Provenance, Guid? NamespaceId, long? Revision, bool? MembersCanQueue, IReadOnlyList<PartyQueueItem>? Queue);
public sealed record PartyPlanningResult(bool Succeeded, string Message, PartyPlanningView? View = null, string? ExportJson = null);
