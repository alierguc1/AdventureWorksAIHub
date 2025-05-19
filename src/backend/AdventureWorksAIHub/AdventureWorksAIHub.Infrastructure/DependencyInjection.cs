using AdventureWorksAIHub.Core.Application.Services;
using AdventureWorksAIHub.Core.Domain.Repositories;
using AdventureWorksAIHub.Infrastructure.Persisitence.Repositories;
using AdventureWorksAIHub.Infrastructure.Persisitence;
using AdventureWorksAIHub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace AdventureWorksAIHub.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Database
            services.AddDbContext<AdventureWorksContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")));

            // Repositories
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IProductVectorRepository, ProductVectorRepository>();
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var redisConnection = configuration.GetConnectionString("Redis");
                return ConnectionMultiplexer.Connect(redisConnection);
            });
            // Services
            services.AddHttpClient<IOllamaService, OllamaService>();
            services.AddScoped<IVectorStoreService, VectorStoreService>();
            services.AddScoped<IRagService, RagService>();

            return services;
        }
    }
}
