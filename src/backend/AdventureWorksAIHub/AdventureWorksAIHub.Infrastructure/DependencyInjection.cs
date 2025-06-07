using AdventureWorksAIHub.Core.Application.Services;
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
using Qdrant.Client.Grpc;
using Qdrant.Client;
using AdventureWorksAIHub.Core.Domain.Repositories.Product;

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
            services.AddSingleton<QdrantClient>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();

                var host = configuration.GetValue<string>("VectorStore:Host") ?? "localhost";
                var port = configuration.GetValue<int>("VectorStore:Port", 6334);
                var apiKey = configuration.GetValue<string>("VectorStore:ApiKey");
                var useHttps = configuration.GetValue<bool>("VectorStore:Https", false);

                if (string.IsNullOrEmpty(apiKey))
                {
                    return new QdrantClient(host, port, https: useHttps);
                }

                return new QdrantClient(host, port, https: useHttps, apiKey: apiKey);
            });
            // Services
            services.AddHttpClient<IOllamaService, OllamaService>();
            services.AddScoped<IVectorStoreService, VectorStoreService>();
            services.AddScoped<IRagService, RagService>();

            return services;
        }
    }
}
