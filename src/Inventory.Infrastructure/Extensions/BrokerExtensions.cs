using Inventory.Application.Transactions.CreateTransaction;
using Inventory.Infrastructure.Observability;
using Inventory.Infrastructure.RabbitMQ.Consumers;
using Joseco.Communication.External.Contracts.Message;
using Joseco.Communication.External.Contracts.Services;
using Joseco.Communication.External.RabbitMQ.Services;
using Joseco.Communication.External.RabbitMQ;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nur.Store2026.Integration.Catalog;
using Nur.Store2026.Integration.Identity;
using Nur.Store2026.Integration.Orders;

namespace Inventory.Infrastructure.Extensions;

public static class BrokerExtensions
{
    public static IServiceCollection AddRabbitMQ(this IServiceCollection services, IHostEnvironment environment)
    {
        using var serviceProvider = services.BuildServiceProvider();
        var rabbitMqSettings = serviceProvider.GetRequiredService<RabbitMqSettings>();

        bool isWebApp = environment is IWebHostEnvironment;
        services.AddRabbitMQ(rabbitMqSettings);

        if (isWebApp)
        {
            return services;
        }

        services
            .AddRabbitMqConsumer<ProductCreated, ProductCreatedConsumer>("inventory-product-created")
            .AddRabbitMqConsumer<UserCreated, UserCreatedConsumer>("inventory-user-created")
            .AddRabbitMqConsumer<OrderStarted, CreateTransactionHandler>("inventory-order-started");

        services.Decorate<IIntegrationMessageConsumer<ProductCreated>, TracingConsumer<ProductCreated>>();
        services.Decorate<IIntegrationMessageConsumer<UserCreated>, TracingConsumer<UserCreated>>();
        services.Decorate<IIntegrationMessageConsumer<OrderStarted>, TracingConsumer<OrderStarted>>();

        return services;
    }
}
