using Joseco.DDD.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Domain.Transactions.Events
{
    public record TransactionReserved : DomainEvent
    {
        public Guid SourceId { get; init; }
        public string SourceType { get; init; }
    }
}
