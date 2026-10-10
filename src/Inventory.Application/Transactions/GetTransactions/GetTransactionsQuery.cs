using Inventory.Domain.Transactions;
using Joseco.DDD.Core.Results;
using MediatR;

namespace Inventory.Application.Transactions.GetTransactions;

/// <summary>
/// Transactions, newest first. Every filter is optional: type (Entry/Exit), concept, status (Created/Completed/Canceled),
/// source type (orders, purchasing, ...) and creation dates. A date without time includes that whole day.
/// </summary>
public record GetTransactionsQuery(
    string? Type = null,
    string? Concept = null,
    string? Status = null,
    string? SourceType = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedTransactions>>;

public record TransactionSummaryDto(
    Guid Id,
    string Type,
    string? Concept,
    string Status,
    DateTime CreationDate,
    DateTime? CompletedDate,
    DateTime? CancelDate,
    Guid CreatorId,
    Guid? SourceId,
    string? SourceType,
    string? InvoiceNumber,
    string? Notes,
    decimal TotalCost,
    int ItemCount,
    int TotalQuantity);

public record PagedTransactions(IReadOnlyList<TransactionSummaryDto> Items, int Page, int PageSize, int Total);

/// <summary>The validated filters of a transaction listing, with the dates as UTC bounds [From, ToExclusive).</summary>
public record TransactionFilter(
    TransactionType? Type,
    TransactionConcept? Concept,
    TransactionStatus? Status,
    string? SourceType,
    DateTime? From,
    DateTime? ToExclusive,
    int Page,
    int PageSize)
{
    public int Skip => (Page - 1) * PageSize;

    public static Result<TransactionFilter> Create(GetTransactionsQuery query)
    {
        Result<TransactionType?> type = QueryFilters.ParseEnum<TransactionType>(query.Type, "type");
        if (type.IsFailure) return Result.Failure<TransactionFilter>(type.Error);

        Result<TransactionConcept?> concept = QueryFilters.ParseEnum<TransactionConcept>(query.Concept, "concept");
        if (concept.IsFailure) return Result.Failure<TransactionFilter>(concept.Error);

        Result<TransactionStatus?> status = QueryFilters.ParseEnum<TransactionStatus>(query.Status, "status");
        if (status.IsFailure) return Result.Failure<TransactionFilter>(status.Error);

        (DateTime? from, DateTime? to) = QueryFilters.DateRange(query.From, query.To);
        if (from != null && to != null && from >= to)
        {
            return Result.Failure<TransactionFilter>(QueryFilters.InvalidRange());
        }

        return new TransactionFilter(type.Value, concept.Value, status.Value,
            string.IsNullOrWhiteSpace(query.SourceType) ? null : query.SourceType.Trim(),
            from, to, Math.Max(1, query.Page), Math.Clamp(query.PageSize, 1, 100));
    }
}

public static class QueryFilters
{
    /// <summary>An optional enum filter given by name (case-insensitive); numbers are not accepted.</summary>
    public static Result<TEnum?> ParseEnum<TEnum>(string? value, string name) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success<TEnum?>(null);
        }
        if (value.Trim().All(char.IsLetter) && Enum.TryParse(value.Trim(), ignoreCase: true, out TEnum parsed))
        {
            return Result.Success<TEnum?>(parsed);
        }
        return Result.Failure<TEnum?>(new Error("Query.InvalidFilter",
            $"'{value}' is not a valid {name}. Use one of: {string.Join(", ", Enum.GetNames<TEnum>())}.", ErrorType.Validation));
    }

    /// <summary>
    /// UTC bounds [from, toExclusive). Dates without a time are whole days, so <c>to=2026-10-10</c> includes the 10th.
    /// Dates without a zone are taken as UTC.
    /// </summary>
    public static (DateTime? From, DateTime? ToExclusive) DateRange(DateTime? from, DateTime? to)
    {
        DateTime? start = from.HasValue ? AsUtc(from.Value) : null;
        DateTime? end = null;
        if (to.HasValue)
        {
            DateTime value = AsUtc(to.Value);
            end = value.TimeOfDay == TimeSpan.Zero ? value.AddDays(1) : value;
        }
        return (start, end);
    }

    public static Error InvalidRange() =>
        new("Query.InvalidDateRange", "'from' must be earlier than 'to'.", ErrorType.Validation);

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
