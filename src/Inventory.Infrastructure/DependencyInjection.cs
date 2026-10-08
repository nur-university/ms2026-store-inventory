using Inventory.Infrastructure.Extensions;
using Joseco.Communication.External.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nur.Store2026.Security;
using System.Reflection;

namespace Inventory.Infrastructure;

public static class DependencyInjection
{

    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceName)
    {
        services.AddMediatR(config =>
                config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())
            );

        services.AddSecrets(configuration, environment)
            .AddObservability(environment, serviceName)
            .AddPersistence()
            .AddSecurity(environment)
            .AddRabbitMQ(environment)
            ;


        return services;
    }

    

    

    

}
