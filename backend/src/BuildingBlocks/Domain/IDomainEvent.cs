namespace UGTM.BuildingBlocks.Domain;

/// <summary>
/// A fact that already happened inside an aggregate. Kept free of any messaging-library
/// dependency; AppDbContextBase (Infrastructure) wraps and publishes these via MediatR
/// after SaveChangesAsync commits, so handlers in other modules can react without the
/// raising module calling them directly.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}
