using Inventory.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Inventory.Infrastructure.Observability;

/// <summary>
/// Business metrics of the stock movements (meter <c>Store.Inventory</c>). Taken from the status changes the unit
/// of work is about to persist and recorded only after <c>SaveChanges</c> succeeded.
/// </summary>
internal static class InventoryMetrics
{
    public const string MeterName = "Store.Inventory";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Transitions = Meter.CreateCounter<long>("store.inventory.transactions",
        description: "Inventory transaction status changes; previous_status is 'none' for a newly created one.");

    private static readonly Counter<long> Units = Meter.CreateCounter<long>("store.inventory.units",
        description: "Units that went in or out of stock when a transaction completed, by concept.");

    private static readonly Histogram<double> PickupTime = Meter.CreateHistogram<double>("store.inventory.sale_pickup_time",
        unit: "s", description: "Time from the reservation of a sale until the customer picked the goods up.");

    internal readonly record struct Transition(
        string From, string To, string Concept, string Type, long Units, double? PickupSeconds);

    /// <summary>Reads the status changes staged in the change tracker. Never throws: metrics must not break a commit.</summary>
    public static IReadOnlyList<Transition> Capture(ChangeTracker tracker)
    {
        var transitions = new List<Transition>();
        try
        {
            foreach (EntityEntry<Transaction> entry in tracker.Entries<Transaction>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified))
                {
                    continue;
                }

                TransactionStatus? from = entry.State == EntityState.Added
                    ? null
                    : (TransactionStatus)entry.Property(nameof(Transaction.Status)).OriginalValue!;
                Transaction transaction = entry.Entity;

                if (from == transaction.Status)
                {
                    continue;
                }

                bool completed = transaction.Status == TransactionStatus.Completed;
                bool pickedUpSale = completed
                    && transaction.Concept == TransactionConcept.Sale
                    && transaction.CompletedDate.HasValue;

                transitions.Add(new Transition(
                    from?.ToString() ?? "none",
                    transaction.Status.ToString(),
                    transaction.Concept?.ToString() ?? "Unknown",
                    transaction.Type.ToString(),
                    completed ? transaction.Items.Sum(i => (long)i.Quantity.Value) : 0,
                    pickedUpSale
                        ? Math.Max(0, (transaction.CompletedDate!.Value - transaction.CreationDate).TotalSeconds)
                        : null));
            }
        }
        catch
        {
            transitions.Clear();
        }

        return transitions;
    }

    public static void Record(IReadOnlyList<Transition> transitions)
    {
        foreach (Transition t in transitions)
        {
            Transitions.Add(1, new TagList
            {
                { "status", t.To }, { "previous_status", t.From }, { "concept", t.Concept }, { "type", t.Type }
            });

            if (t.To == nameof(TransactionStatus.Completed))
            {
                Units.Add(t.Units, new TagList { { "concept", t.Concept }, { "type", t.Type } });
            }

            if (t.PickupSeconds.HasValue)
            {
                PickupTime.Record(t.PickupSeconds.Value);
            }
        }
    }
}
