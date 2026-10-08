using Inventory.Application.Items.CreateItem;
using Joseco.Communication.External.Contracts.Services;
using Joseco.Communication.External.RabbitMQ.Services;
using MediatR;
using Nur.Store2026.Integration.Catalog;

namespace Inventory.Infrastructure.RabbitMQ.Consumers;

public class ProductCreatedConsumer(IMediator mediator) 
    : IIntegrationMessageConsumer<ProductCreated>
{
    public async Task HandleAsync(ProductCreated message, CancellationToken cancellationToken)
    {
        CreateItemCommand command = new(
            message.ProductId,
            message.Name
        );

        await mediator.Send(command, cancellationToken);
    }
}
