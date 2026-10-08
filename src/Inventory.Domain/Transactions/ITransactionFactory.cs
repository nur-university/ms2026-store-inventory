using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Domain.Transactions;

public interface ITransactionFactory
{
    Transaction CreateEntryTransaction(Guid userCreatorId, List<(Guid itemId, int quantity, decimal unitaryCost)> items, Guid? sourceId = null, string? sourceType = null);
    Transaction CreateExitTransaction(Guid userCreatorId, List<(Guid itemId, int quantity)> items, Guid? sourceId = null, string? sourceType = null) ;
}
