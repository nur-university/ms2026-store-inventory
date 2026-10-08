using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Inventory.Domain.Transactions.Exceptions;
using MediatR;
using Joseco.DDD.Core.Abstractions;
using Joseco.Communication.External.Contracts.Services;
using Nur.Store2026.Integration.Orders;
using Inventory.Domain.Transactions.Events;
using Joseco.DDD.Core.Results;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Transactions.CreateTransaction;

public class CreateTransactionHandler : IRequestHandler<CreateTransactionCommand, Guid>,
                                        IIntegrationMessageConsumer<OrderStarted>
{
    private readonly ITransactionFactory _transactionFactory;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxService<DomainEvent> _outboxService;
    private readonly ILogger<CreateTransactionHandler> _logger;

    public CreateTransactionHandler(ITransactionFactory transactionFactory,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        IItemRepository itemRepository,
        ILogger<CreateTransactionHandler> logger,
        IOutboxService<DomainEvent> outboxService) {
        _transactionFactory = transactionFactory;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _itemRepository = itemRepository;
        _logger = logger;
        _outboxService = outboxService;
    }

    public async Task<Guid> Handle(CreateTransactionCommand request, CancellationToken cancellationToken) {

        var transaction = request.Type switch
        {
            "Entry" => _transactionFactory.CreateEntryTransaction(
                request.UserCreatorId,
                [.. request.Items.Select(i => (i.ItemId, i.Quantity, i.UnitaryCost))]

            ),
            "Exit" => _transactionFactory.CreateExitTransaction(
                request.UserCreatorId,
                [.. request.Items.Select(i => (i.ItemId, i.Quantity))],
                request.SourceId,
                request.SourceType
            ),
            _ => throw new TransactionCreationException("Invalid transaction type")
        };

        //foreach (var item in request.Items)
        //{
        //    var storeItem = await _itemRepository.GetByIdAsync(item.ItemId, true);
        //    if (storeItem == null)
        //    {
        //        throw new TransactionCreationException("Item not found");
        //    }

        //    transaction.AddItem(item.ItemId, item.Quantity, item.UnitaryCost);
        //}

        await _transactionRepository.AddAsync(transaction);

        await _unitOfWork.CommitAsync(cancellationToken);

        return transaction.Id;
    }

    public async Task HandleAsync(OrderStarted message, CancellationToken cancellationToken) {
        List<CreateTransacionItemCommand> items = message.Items.Select(i => new CreateTransacionItemCommand(i.ProductId, i.Quantity, 0)).ToList();

        // A duplicated OrderStarted must not reserve the stock twice.
        if (await _transactionRepository.GetBySourceAsync(message.OrderId, "orders") != null)
        {
            _logger.LogInformation("Stock for order {OrderId} was already reserved; ignoring OrderStarted", message.OrderId);
            return;
        }

        string? reason = null;
        try
        {
            _logger.LogInformation("Consuming OrderStarted message");

            CreateTransactionCommand command = new(new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                "Exit",
                items,
                message.OrderId,
                "orders");

            await Handle(command, cancellationToken);
        }
        catch (DomainException ex)
        {
            _logger.LogError(ex, "Domain error processing OrderStarted message: {Message}", ex.Message);
            var error = ex.Error;
            reason = $"{error.Code}: {error.Description}";
        }
        catch (TransactionCreationException ex)
        {
            _logger.LogError(ex, "Error processing OrderStarted message: {Message}", ex.Message);
            reason = ex.Message;
        }
        // Anything else (database down, concurrency conflict, ...) is not a business rejection: it propagates so the
        // message is retried and, if it keeps failing, ends up in the error queue instead of failing the order.

        if (!string.IsNullOrEmpty(reason))
        {
            ReservationTransactionFailed reserveTransactionFailed =
                new(message.OrderId, reason);

            OutboxMessage<DomainEvent> outboxMessage = new(reserveTransactionFailed);

            await _outboxService.AddAsync(outboxMessage);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
    }
}