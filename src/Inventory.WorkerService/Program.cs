using Inventory.Application;
using Inventory.Infrastructure;
using Nur.Store2026.Observability;
using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.EFCore;

var builder = Host.CreateApplicationBuilder(args);

string serviceName = "inventory.worker-service";

builder.UseLogging(serviceName, builder.Configuration);

builder.Services.AddApplication()
                .AddInfrastructure(builder.Configuration, builder.Environment, serviceName);
builder.Services.AddOutboxBackgroundService<DomainEvent>();

var host = builder.Build();
host.Run();
