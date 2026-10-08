using Inventory.Domain.Transactions.Events;
using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using MediatR;
using Nur.Store2026.Integration.Inventory;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Application.Transactions.EventHandlers;

internal class SaveToOutboxWhenTransactionReserved : INotificationHandler<TransactionReserved>
{
    private readonly IOutboxService<DomainEvent> _otuboxService;

    public SaveToOutboxWhenTransactionReserved(IOutboxService<DomainEvent> otuboxService) {
        _otuboxService = otuboxService;
    }

    public async Task Handle(TransactionReserved domainEvent, CancellationToken cancellationToken) {

        OutboxMessage<DomainEvent> outboxMessage = new(domainEvent);

        await _otuboxService.AddAsync(outboxMessage);
    }
}
