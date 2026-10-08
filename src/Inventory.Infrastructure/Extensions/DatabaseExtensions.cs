using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Inventory.Domain.Users;
using Inventory.Infrastructure.Persistence.DomainModel;
using Inventory.Infrastructure.Persistence.Repositories;
using Inventory.Infrastructure.Persistence.StoredModel;
using Inventory.Infrastructure.Persistence;
using Joseco.DDD.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Joseco.Outbox.EFCore.Persistence;
using Joseco.Outbox.EFCore;
using Joseco.Outbox.Contracts.Service;

namespace Inventory.Infrastructure.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        var databaseSettings = services.BuildServiceProvider().GetRequiredService<DataBaseSettings>();
        var dbConnectionString = databaseSettings.ConnectionString;

        services.AddDbContext<StoredDbContext>(context =>
                context.UseNpgsql(dbConnectionString));
        services.AddDbContext<DomainDbContext>(context =>
                context.UseNpgsql(dbConnectionString));

        services.AddScoped<IDatabase, StoredDbContext>();

        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IOutboxDatabase<DomainEvent>, UnitOfWork>();
        services.AddOutbox<DomainEvent>();

        services.Decorate<IOutboxService<DomainEvent>, OutboxTracingService<DomainEvent>>();

        return services;
    }
}
