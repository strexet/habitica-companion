namespace Habitica.Domain.Tasks;

public sealed record TaskCollectionSnapshot(
    DateTimeOffset RetrievedAtUtc,
    IReadOnlyList<TaskSnapshot> Items);
