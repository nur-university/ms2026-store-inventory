using Joseco.DDD.Core.Results;
using MediatR;

namespace Inventory.Application.Items.GetItemMovements;

/// <summary>
/// The kardex of an item: its completed movements in chronological order (by completion date), each with the
/// running balance of units. Pending transactions do not move the stock and are not listed (a pending write-off
/// or sale only shows up in <c>Reserved</c>). A date without time includes that whole day.
/// </summary>
public record GetItemMovementsQuery(Guid ItemId, DateTime? From = null, DateTime? To = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<ItemKardexDto>>;

/// <param name="UnitaryCost">The cost an entry came in at; null for an exit (it leaves at the average cost).</param>
/// <param name="Balance">Units in stock right after this movement.</param>
public record ItemMovementDto(
    DateTime Date,
    Guid TransactionId,
    string Type,
    string? Concept,
    Guid? SourceId,
    string? SourceType,
    string? InvoiceNumber,
    string? Notes,
    int QuantityIn,
    int QuantityOut,
    decimal? UnitaryCost,
    int Balance);

/// <param name="BalanceBefore">Units in stock before the first movement of this page.</param>
/// <param name="BalanceAfter">Units in stock after the last movement of this page.</param>
public record ItemKardexDto(
    Guid ItemId,
    string ItemName,
    int Stock,
    int Reserved,
    int Available,
    decimal UnitaryCost,
    int BalanceBefore,
    int BalanceAfter,
    IReadOnlyList<ItemMovementDto> Movements,
    int Page,
    int PageSize,
    int Total);

/// <summary>A completed movement of the item, as read from the database, before the balance is accumulated.</summary>
public record ItemMovementRow(
    DateTime Date,
    Guid TransactionId,
    string Type,
    string? Concept,
    Guid? SourceId,
    string? SourceType,
    string? InvoiceNumber,
    string? Notes,
    int Quantity,
    decimal UnitaryCost);

public static class Kardex
{
    public const string Entry = "Entry";

    /// <summary>Adds the running balance to chronologically ordered movements, starting from <paramref name="balanceBefore"/>.</summary>
    public static IReadOnlyList<ItemMovementDto> Accumulate(int balanceBefore, IEnumerable<ItemMovementRow> rows)
    {
        int balance = balanceBefore;
        List<ItemMovementDto> movements = [];
        foreach (ItemMovementRow row in rows)
        {
            bool isEntry = row.Type == Entry;
            balance += isEntry ? row.Quantity : -row.Quantity;
            movements.Add(new ItemMovementDto(row.Date, row.TransactionId, row.Type, row.Concept, row.SourceId, row.SourceType,
                row.InvoiceNumber, row.Notes,
                isEntry ? row.Quantity : 0,
                isEntry ? 0 : row.Quantity,
                isEntry ? row.UnitaryCost : null,
                balance));
        }
        return movements;
    }
}
