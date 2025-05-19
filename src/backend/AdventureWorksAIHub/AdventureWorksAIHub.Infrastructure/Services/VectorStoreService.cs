using AdventureWorksAIHub.Core.Application.Dtos;
using AdventureWorksAIHub.Core.Application.Services;
using AdventureWorksAIHub.Core.Domain.Entities;
using AdventureWorksAIHub.Core.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Infrastructure.Services
{
    public class VectorStoreService : IVectorStoreService
    {
        private readonly IProductRepository _productRepository;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly IOllamaService _ollamaService;
        private readonly ILogger<VectorStoreService> _logger;
        private readonly string _keyPrefix;
        private readonly int _batchSize;
        private readonly bool _isRedisVectorSearchEnabled;

        public VectorStoreService(
            IProductRepository productRepository,
            IConnectionMultiplexer redis,
            IOllamaService ollamaService,
            ILogger<VectorStoreService> logger,
            IConfiguration configuration)
        {
            _productRepository = productRepository;
            _redis = redis;
            _db = _redis.GetDatabase();
            _ollamaService = ollamaService;
            _logger = logger;

            // Redis ile ilgili ayarları configuration'dan al
            _keyPrefix = configuration.GetValue<string>("VectorStore:KeyPrefix") ?? "product_vector:";
            _batchSize = configuration.GetValue<int>("VectorStore:BatchSize", 10);

            // Başlangıçta RedisSearch'ü kullanmadan devam edelim
            _isRedisVectorSearchEnabled = false;

            _logger.LogInformation("VectorStoreService initialized with Redis. KeyPrefix: {KeyPrefix}, VectorSearch: {VectorSearchEnabled}",
                _keyPrefix, _isRedisVectorSearchEnabled);
        }

        public async Task IndexProductDescriptionsAsync()
        {
            _logger.LogInformation("Starting product descriptions indexing in Redis");

            var products = await _productRepository.GetProductsWithDescriptionsAsync();
            int count = 0;
            int total = products.Count();

            _logger.LogInformation("Found {Total} products to index", total);

            foreach (var product in products)
            {
                if (product.ProductDescription == null ||
                    string.IsNullOrEmpty(product.ProductDescription.Description))
                {
                    continue;
                }

                try
                {
                    var text = $"Product: {product.Name}. Description: {product.ProductDescription.Description}";
                    var embedding = await _ollamaService.EmbedTextAsync(text);
                    var embeddingJson = JsonSerializer.Serialize(embedding);

                    // Redis key
                    var key = $"{_keyPrefix}{product.ProductID}";

                    // Hash alanları oluştur
                    var hashEntries = new HashEntry[]
                    {
                        new HashEntry("productId", product.ProductID.ToString()),
                        new HashEntry("text", text),
                        new HashEntry("embedding", embeddingJson)
                    };

                    // Redis'e kaydet
                    await _db.HashSetAsync(key, hashEntries);

                    count++;

                    if (count % _batchSize == 0)
                    {
                        _logger.LogInformation($"Indexed {count}/{total} products so far ({(count * 100.0 / total):F1}%)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error indexing product {product.ProductID}");
                }
            }

            _logger.LogInformation($"Completed indexing {count}/{total} products in Redis");
        }

        public async Task<List<SearchResultDto>> FindSimilarProductsAsync(string query, int limit = 5)
        {
            _logger.LogInformation("Finding products similar to: '{Query}', limit: {Limit}", query, limit);

            var queryEmbedding = await _ollamaService.EmbedTextAsync(query);
            _logger.LogDebug("Generated embedding for query with dimension {Dimension}", queryEmbedding.Length);

            // Vector search kullanım durumuna göre yöntem seç
            if (_isRedisVectorSearchEnabled)
            {
                try
                {
                    // Bu kısmı şimdilik devre dışı bırakalım, önce basit Redis kullanımı yapalım
                    _logger.LogWarning("Redis Vector Search is enabled but not yet implemented. Using fallback method.");
                    return await FindSimilarProductsFallbackAsync(queryEmbedding, limit);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error using Redis Vector Search. Falling back to in-memory calculation.");
                    return await FindSimilarProductsFallbackAsync(queryEmbedding, limit);
                }
            }
            else
            {
                return await FindSimilarProductsFallbackAsync(queryEmbedding, limit);
            }
        }

        // Bellek içi benzerlik hesaplama metodu (Redis Vector Search olmadan)
        private async Task<List<SearchResultDto>> FindSimilarProductsFallbackAsync(float[] queryEmbedding, int limit = 5)
        {
            try
            {
                _logger.LogInformation("Using in-memory similarity calculation");

                // Tüm ürün vektörlerini Redis'ten al
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(pattern: $"{_keyPrefix}*").ToArray();

                _logger.LogDebug("Found {Count} product keys in Redis", keys.Length);

                var results = new List<SearchResultDto>();

                foreach (var key in keys)
                {
                    try
                    {
                        var hashEntries = await _db.HashGetAllAsync(key);
                        var dict = hashEntries.ToDictionary(he => he.Name.ToString(), he => he.Value.ToString());

                        if (dict.ContainsKey("productId") && dict.ContainsKey("text") && dict.ContainsKey("embedding"))
                        {
                            var productId = int.Parse(dict["productId"]);
                            var text = dict["text"];
                            var productVector = JsonSerializer.Deserialize<float[]>(dict["embedding"]);

                            var similarity = CosineSimilarity(queryEmbedding, productVector);

                            results.Add(new SearchResultDto
                            {
                                ProductID = productId,
                                Text = text,
                                Similarity = similarity
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error calculating similarity for product key {key}");
                    }
                }

                return results
                    .OrderByDescending(r => r.Similarity)
                    .Take(limit)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fallback similarity calculation: {Error}", ex.Message);
                return new List<SearchResultDto>();
            }
        }

        // Kosinüs benzerliği hesaplama
        private float CosineSimilarity(float[] a, float[] b)
        {
            float dotProduct = 0;
            float normA = 0;
            float normB = 0;

            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            return normA > 0 && normB > 0
                ? dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB))
                : 0;
        }
    }
}