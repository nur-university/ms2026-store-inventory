using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Inventory.Domain.Transactions.Exceptions;
using Inventory.Domain.Users;
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

public class CreateTransactionHandler : IRequestHandler<CreateTransactionCommand, Result<Guid>>,
                                        IIntegrationMessageConsumer<OrderStarted>
{
    private readonly ITransactionFactory _transactionFactory;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRepository _userRepository;
    // Creator used when the order carries no user, or a user inventory does not know yet.
    private static readonly Guid SystemUserId = new("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private readonly IOutboxService<DomainEvent> _outboxService;
    private readonly ILogger<CreateTransactionHandler> _logger;

    public CreateTransactionHandler(ITransactionFactory transactionFactory,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        IItemRepository itemRepository,
        ILogger<CreateTransactionHandler> logger,
        IOutboxService<DomainEvent> outboxService,
        IUserRepository userRepository) {
        _userRepository = userRepository;
        _transactionFactory = transactionFactory;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _itemRepository = itemRepository;
        _logger = logger;
        _outboxService = outboxService;
    }

    /// <summary>A write-off or an opening balance created by hand; it stays pending until it is approved or rejected.</summary>
    public async Task<Result<Guid>> Handle(CreateTransactionCommand request, CancellationToken cancellationToken) {

        Result<TransactionConcept> concept = ParseConcept(request.Concept);
        if (concept.IsFailure)
        {
            return Result.Failure<Guid>(concept.Error);
        }
        if (!concept.Value.CanBeCreatedManually())
        {
            return Result.Failure<Guid>(TransactionErrors.ConceptNotManual(concept.Value));
        }

        bool isEntry = concept.Value.TypeOf() == TransactionType.Entry;
        Error? invalidItems = await ValidateItemsAsync(request.Items, isEntry);
        if (invalidItems != null)
        {
            return Result.Failure<Guid>(invalidItems);
        }

        Guid creatorId = await ResolveCreatorAsync(request.UserCreatorId);

        try
        {
            Transaction transaction = isEntry
                ? _transactionFactory.CreateEntryTransaction(creatorId, concept.Value,
                    [.. request.Items.Select(i => (i.ItemId, i.Quantity, i.UnitaryCost))], request.Notes)
                : _transactionFactory.CreateExitTransaction(creatorId, concept.Value,
                    [.. request.Items.Select(i => (i.ItemId, i.Quantity))], request.Notes);

            await _transactionRepository.AddAsync(transaction);

            // A write-off reserves the stock now (not enough available stock is a business error).
            await _unitOfWork.CommitAsync(cancellationToken);

            return transaction.Id;
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(ex.Error);
        }
    }

    private static Result<TransactionConcept> ParseConcept(string? value)
    {
        // Names only: Enum.TryParse would also accept numbers such as "3".
        if (string.IsNullOrWhiteSpace(value) || !value.Trim().All(char.IsLetter)
            || !Enum.TryParse(value.Trim(), ignoreCase: true, out TransactionConcept concept))
        {
            return Result.Failure<TransactionConcept>(TransactionErrors.InvalidConcept(value));
        }
        return concept;
    }

    private async Task<Error?> ValidateItemsAsync(ICollection<CreateTransacionItemCommand>? items, bool isEntry)
    {
        if (items == null || items.Count == 0)
        {
            return TransactionErrors.NoItems();
        }

        HashSet<Guid> seen = [];
        foreach (CreateTransacionItemCommand item in items)
        {
            if (!seen.Add(item.ItemId))
            {
                return TransactionErrors.DuplicatedItem(item.ItemId);
            }
            if (item.Quantity <= 0)
            {
                return TransactionErrors.InvalidQuantity(item.ItemId);
            }
            // An entry sets the cost the goods are valued at; an exit leaves at the average cost, so its cost is ignored.
            if (isEntry && item.UnitaryCost <= 0)
            {
                return TransactionErrors.InvalidCost(item.ItemId);
            }
        }

        ICollection<Item> known = await _itemRepository.GetItemsFromId(seen, readOnly: true);
        Guid? unknown = seen.Cast<Guid?>().FirstOrDefault(id => known.All(k => k.Id != id));
        return unknown.HasValue ? TransactionErrors.UnknownItem(unknown.Value) : null;
    }

    public async Task HandleAsync(OrderStarted message, CancellationToken cancellationToken) {
        // A duplicated OrderStarted must not reserve the stock twice.
        if (await _transactionRepository.GetBySourceAsync(message.OrderId, TransactionSources.Orders) != null)
        {
            _logger.LogInformation("Stock for order {OrderId} was already reserved; ignoring OrderStarted", message.OrderId);
            return;
        }

        string? reason = null;
        try
        {
            _logger.LogInformation("Consuming OrderStarted message");

            Guid creatorId = await ResolveCreatorAsync(message.UserId);

            Transaction transaction = _transactionFactory.CreateExitTransaction(creatorId,
                TransactionConcept.Sale,
                [.. message.Items.Select(i => (i.ProductId, i.Quantity))],
                sourceId: message.OrderId,
                sourceType: TransactionSources.Orders);

            await _transactionRepository.AddAsync(transaction);

            await _unitOfWork.CommitAsync(cancellationToken);
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

    private async Task<Guid> ResolveCreatorAsync(Guid? userId)
    {
        if (userId.HasValue && await _userRepository.GetByIdAsync(userId.Value, true) != null)
        {
            return userId.Value;
        }

        if (userId.HasValue)
        {
            _logger.LogWarning("User {UserId} is not known by inventory yet; using the system user as creator", userId);
        }

        return SystemUserId;
    }
}
