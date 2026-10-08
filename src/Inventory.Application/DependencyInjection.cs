using Inventory.Application.Behaviors;
using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Inventory.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services) {
            services.AddMediatR(config =>
            {
                config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                config.AddOpenBehavior(typeof(RequestLoggingPipelineBehavior<,>));
            });

            services.AddSingleton<ITransactionFactory, TransactionFactory>();
            services.AddSingleton<IItemFactory, ItemFactory>();

            return services;
        }

    }
}
