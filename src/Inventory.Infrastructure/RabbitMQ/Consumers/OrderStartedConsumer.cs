using Inventory.Application.Transactions.CreateTransaction;
using Inventory.Domain.Transactions.Events;
using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using MediatR;
using Microsoft.Extensions.Logging;
using Nur.Store2026.Integration.Orders;

namespace Inventory.Infrastructure.RabbitMQ.Consumers;

public class OrderStartedConsumer : IIntegrationMessageConsumer<OrderStarted>
{
    private readonly IMediator _mediator;
    private readonly ILogger<OrderStartedConsumer> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxService<DomainEvent> _outboxService;

    public OrderStartedConsumer(IMediator mediator, ILogger<OrderStartedConsumer> logger, 
        IUnitOfWork unitOfWork, IOutboxService<DomainEvent> outboxService)
    {
        _mediator = mediator;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _outboxService = outboxService;
    }

    public async Task HandleAsync(OrderStarted message, CancellationToken cancellationToken)
    {
        List<CreateTransacionItemCommand> items = message.Items.Select(i => new CreateTransacionItemCommand(i.ProductId, i.Quantity, 0)).ToList();

        string reason = null;
        try
        {
            _logger.LogInformation("Consuming OrderStarted message");

            CreateTransactionCommand command = new(new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                "Exit",
                items,
                message.OrderId,
                "orders");

            await _mediator.Send(command, cancellationToken);
        }
        catch(DomainException ex)
        {
            _logger.LogError(ex, "Domain error processing OrderStarted message: {Message}", ex.Message);
            var error = ex.Error;
            reason = $"{error.Code}: {error.Description}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderStarted message: {Message}", ex.Message);
            reason = ex.Message;
        }
        if (!string.IsNullOrEmpty(reason))
        {
            ReserveTransactionFailed reserveTransactionFailed =
                new(message.OrderId, reason);

            OutboxMessage<DomainEvent> outboxMessage = new(reserveTransactionFailed);

            await _outboxService.AddAsync(outboxMessage);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
    }
}
