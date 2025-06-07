using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NRedisStack.Search.Literals.Enums;
using NRedisStack.Search;
using NRedisStack;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NRedisStack.Search.Schema;
using Microsoft.Extensions.Configuration;
using NRedisStack.RedisStackCommands;
using Qdrant.Client.Grpc;
using Qdrant.Client;
using AdventureWorksAIHub.Core.Domain.Repositories.Products;
using AdventureWorksAIHub.Core.Domain.Entities.Products;

namespace AdventureWorksAIHub.Infrastructure.Persisitence.Repositories.Products
{
    public class ProductVectorRepository : IProductVectorRepository
    {
        private readonly QdrantClient _qdrantClient;
        private readonly ILogger<ProductVectorRepository> _logger;
        private readonly string _collectionName;
        private readonly int _vectorSize;

        public ProductVectorRepository(
            QdrantClient qdrantClient,
            ILogger<ProductVectorRepository> logger,
            IConfiguration configuration)
        {
            _qdrantClient = qdrantClient;
            _logger = logger;

            // Configuration'dan ayarları oku
            var prefix = configuration.GetValue<string>("VectorStore:IndexPrefix") ?? "product_vectors";
            _collectionName = $"{prefix}";

            // Gerçek embedding boyutunu dinamik olarak belirle
            _vectorSize = configuration.GetValue("VectorStore:EmbeddingDimension", 3584); // 3584 olarak güncelledik

            EnsureCollectionExistsAsync().GetAwaiter().GetResult();
        }

        private async Task EnsureCollectionExistsAsync()
        {
            try
            {
                _logger.LogInformation("Checking if Qdrant collection exists: {CollectionName}", _collectionName);

                var collections = await _qdrantClient.ListCollectionsAsync();
                var collectionExists = collections.Any(c => c == _collectionName);

                if (!collectionExists)
                {
                    _logger.LogInformation("Creating Qdrant collection: {CollectionName}", _collectionName);

                    await _qdrantClient.CreateCollectionAsync(
                        collectionName: _collectionName,
                        vectorsConfig: new VectorParams
                        {
                            Size = (ulong)_vectorSize,
                            Distance = Distance.Cosine
                        });

                    _logger.LogInformation("Successfully created Qdrant collection: {CollectionName} with dimension {Dimension}",
                        _collectionName, _vectorSize);
                }
                else
                {
                    _logger.LogInformation("Qdrant collection {CollectionName} already exists", _collectionName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring Qdrant collection exists: {CollectionName}", _collectionName);
                throw;
            }
        }

        public async Task<IEnumerable<ProductVector>> GetAllAsync()
        {
            try
            {
                _logger.LogInformation("Getting all product vectors from Qdrant");

                var scrollResponse = await _qdrantClient.ScrollAsync(
                    collectionName: _collectionName,
                    payloadSelector: true,
                    limit: 1000
                );
                var results = new List<ProductVector>();

                foreach (var point in scrollResponse.Result)
                {
                    var productVector = ConvertToProductVector(point);
                    if (productVector != null)
                    {
                        results.Add(productVector);
                    }
                }

                _logger.LogInformation("Retrieved {Count} product vectors from Qdrant", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all product vectors from Qdrant");
                throw;
            }
        }

        public async Task<ProductVector> GetByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting product vector by ID: {Id}", id);

                // API uyumluluğu için basit yaklaşım: tüm vektörleri getir ve filtrele
                var allVectors = await GetAllAsync();
                var result = allVectors.FirstOrDefault(v => v.ProductVectorID == id);

                if (result == null)
                {
                    _logger.LogInformation("No product vector found with ID: {Id}", id);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product vector by ID: {Id}", id);
                throw;
            }
        }

        public async Task<ProductVector> GetByProductIdAsync(int productId)
        {
            try
            {
                _logger.LogInformation("Getting product vector by Product ID: {ProductId}", productId);

                // Basit yöntem: Tüm pointleri al ve filtrele
                var allPoints = await _qdrantClient.ScrollAsync(
                    collectionName: _collectionName,
                    payloadSelector: true,
                    limit: 10000 // Büyük limit
                );

                var matchingPoint = allPoints.Result.FirstOrDefault(p =>
                {
                    if (p.Payload.ContainsKey("productId"))
                    {
                        var payloadProductId = (int)p.Payload["productId"].IntegerValue;
                        return payloadProductId == productId;
                    }
                    return false;
                });
                if (matchingPoint != null)
                {
                    return ConvertToProductVector(matchingPoint);
                }

                _logger.LogInformation("No product vector found for Product ID: {ProductId}", productId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product vector by Product ID: {ProductId}", productId);
                throw;
            }
        }

        public async Task AddAsync(ProductVector productVector)
        {
            try
            {
                _logger.LogInformation("Adding new product vector for Product ID: {ProductId}", productVector.ProductID);

                // Embedding'i float array'e dönüştür
                var embeddingArray = System.Text.Json.JsonSerializer.Deserialize<float[]>(productVector.Embedding);

                var point = new PointStruct
                {
                    Id = (ulong)productVector.ProductID,
                    Vectors = embeddingArray,
                    Payload =
                    {
                        ["productVectorId"] = productVector.ProductVectorID,
                        ["productId"] = productVector.ProductID,
                        ["text"] = productVector.Text ?? string.Empty,
                        ["embedding"] = productVector.Embedding
                    }
                };

                await _qdrantClient.UpsertAsync(_collectionName, new[] { point });
                _logger.LogInformation("Successfully added product vector for Product ID: {ProductId}", productVector.ProductID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding product vector for Product ID: {ProductId}", productVector.ProductID);
                throw;
            }
        }

        public async Task UpdateAsync(ProductVector productVector)
        {
            try
            {
                _logger.LogInformation("Updating product vector for Product ID: {ProductId}", productVector.ProductID);

                // Qdrant'ta update işlemi upsert ile yapılır
                await AddAsync(productVector);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product vector for Product ID: {ProductId}", productVector.ProductID);
                throw;
            }
        }

        public async Task SaveChangesAsync()
        {
            try
            {
                _logger.LogInformation("Saving changes to Qdrant (changes are saved immediately)");

                // Qdrant'ta değişiklikler anında kaydedilir
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveChangesAsync for Qdrant");
                throw;
            }
        }

        // Vector search ile benzer ürünleri bulma metodu (async version)
        public async Task<List<Tuple<ProductVector, float>>> FindSimilarVectorsAsync(float[] queryEmbedding, int limit = 5)
        {
            try
            {
                _logger.LogInformation("Finding similar vectors in Qdrant with vector search, limit: {Limit}", limit);

                var searchResults = await _qdrantClient.SearchAsync(
                    collectionName: _collectionName,
                    vector: queryEmbedding,
                    limit: (ulong)limit,
                    payloadSelector: true,
                    scoreThreshold: 0.1f
                );

                var results = searchResults.Select(result =>
                {
                    var productVector = ConvertToProductVector(result);
                    return new Tuple<ProductVector, float>(productVector, result.Score);
                }).ToList();

                _logger.LogInformation("Found {Count} similar vectors in Qdrant", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar vectors in Qdrant");
                throw;
            }
        }

        // Mevcut interface'i korumak için sync version
        public List<Tuple<ProductVector, float>> FindSimilarVectors(float[] queryEmbedding, int limit = 5)
        {
            try
            {
                _logger.LogInformation("Finding similar vectors in Qdrant with vector search (sync), limit: {Limit}", limit);

                var searchResults = _qdrantClient.SearchAsync(
                    collectionName: _collectionName,
                    vector: queryEmbedding,
                    limit: (ulong)limit,
                    payloadSelector: true,
                    scoreThreshold: 0.1f
                ).GetAwaiter().GetResult();

                var results = searchResults.Select(result =>
                {
                    var productVector = ConvertToProductVector(result);
                    return new Tuple<ProductVector, float>(productVector, result.Score);
                }).ToList();

                _logger.LogInformation("Found {Count} similar vectors in Qdrant", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar vectors in Qdrant (sync)");
                throw;
            }
        }

        private ProductVector ConvertToProductVector(RetrievedPoint point)
        {
            try
            {
                var payload = point.Payload;

                if (!payload.ContainsKey("productId") || !payload.ContainsKey("text") || !payload.ContainsKey("embedding"))
                {
                    _logger.LogWarning("Incomplete product vector data found in Qdrant for point ID: {PointId}", point.Id);
                    return null;
                }

                var productVector = new ProductVector
                {
                    ProductID = (int)payload["productId"].IntegerValue,
                    Text = payload["text"].StringValue,
                    Embedding = payload["embedding"].StringValue
                };

                if (payload.ContainsKey("productVectorId") && payload["productVectorId"].IntegerValue > 0)
                {
                    productVector.ProductVectorID = (int)payload["productVectorId"].IntegerValue;
                }

                return productVector;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting Qdrant point to ProductVector");
                return null;
            }
        }

        private ProductVector ConvertToProductVector(ScoredPoint point)
        {
            try
            {
                var payload = point.Payload;

                if (!payload.ContainsKey("productId") || !payload.ContainsKey("text") || !payload.ContainsKey("embedding"))
                {
                    _logger.LogWarning("Incomplete product vector data found in Qdrant for point ID: {PointId}", point.Id);
                    return null;
                }

                var productVector = new ProductVector
                {
                    ProductID = (int)payload["productId"].IntegerValue,
                    Text = payload["text"].StringValue,
                    Embedding = payload["embedding"].StringValue
                };

                if (payload.ContainsKey("productVectorId") && payload["productVectorId"].IntegerValue > 0)
                {
                    productVector.ProductVectorID = (int)payload["productVectorId"].IntegerValue;
                }

                return productVector;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting Qdrant scored point to ProductVector");
                return null;
            }
        }
    }
}