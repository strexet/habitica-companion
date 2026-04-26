namespace Habitica.Domain.Party;

public sealed record PartySnapshot(
    DateTimeOffset RetrievedAtUtc,
    string PartyId,
    string Name,
    string? Summary,
    int MemberCount,
    PartyQuestSnapshot? Quest);

public sealed record PartyQuestSnapshot(
    string? Key,
    bool IsActive,
    decimal ProgressUp,
    decimal ProgressDown,
    int ParticipantCount);
