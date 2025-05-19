using AdventureWorksAIHub.Core.Domain.Entities;
using AdventureWorksAIHub.Core.Domain.Repositories;
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

namespace AdventureWorksAIHub.Infrastructure.Persisitence.Repositories
{
    public class ProductVectorRepository : IProductVectorRepository
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly ILogger<ProductVectorRepository> _logger;
        private readonly string _indexName;
        private readonly string _keyPrefix;
        private readonly int _embeddingDimension;

        public ProductVectorRepository(IConnectionMultiplexer redis, ILogger<ProductVectorRepository> logger, IConfiguration configuration)
        {
            _redis = redis;
            _db = _redis.GetDatabase();
            _logger = logger;

            // Configuration'dan ayarları oku
            var prefix = configuration.GetValue<string>("VectorStore:IndexPrefix") ?? "product_vectors";
            _indexName = $"{prefix}_idx";
            _keyPrefix = $"{prefix}:";
            _embeddingDimension = configuration.GetValue<int>("VectorStore:EmbeddingDimension", 1536);

            EnsureIndexExists();
        }

        private void EnsureIndexExists()
        {
            try
            {
                _logger.LogInformation("Checking if Redis index exists: {IndexName}", _indexName);
                _db.Execute("FT.INFO", _indexName);
                _logger.LogInformation("Redis index {IndexName} already exists", _indexName);
            }
            catch (RedisServerException)
            {
                _logger.LogInformation("Creating Redis vector search index: {IndexName}", _indexName);

                // HNSW Vector Search indeksi oluştur
                var parameters = new List<object>
                {
                    _indexName,
                    "ON", "HASH",
                    "PREFIX", 1, _keyPrefix,
                    "SCHEMA",
                    "productId", "TEXT", "SORTABLE",
                    "productVectorId", "TEXT", "SORTABLE",
                    "text", "TEXT",
                    "embedding", "VECTOR", "HNSW", 6,
                    "TYPE", "FLOAT32",
                    "DIM", _embeddingDimension,
                    "DISTANCE_METRIC", "COSINE",
                    "INITIAL_CAP", 1000,
                    "M", 40,
                    "EF_CONSTRUCTION", 200
                };

                _db.Execute("FT.CREATE", parameters.ToArray());

                _logger.LogInformation("Successfully created Redis vector search index: {IndexName} with dimension {Dimension}",
                    _indexName, _embeddingDimension);
            }
        }

        public async Task<IEnumerable<ProductVector>> GetAllAsync()
        {
            try
            {
                _logger.LogInformation("Getting all product vectors from Redis");

                var results = new List<ProductVector>();

                // Tüm ürün vektörlerini almak için
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(pattern: $"{_keyPrefix}*").ToArray();

                _logger.LogInformation("Found {Count} product vector keys in Redis", keys.Length);

                foreach (var key in keys)
                {
                    var hashEntries = await _db.HashGetAllAsync(key);
                    var productVector = ConvertToProductVector(hashEntries);
                    if (productVector != null)
                    {
                        results.Add(productVector);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all product vectors from Redis");
                throw;
            }
        }

        public async Task<ProductVector> GetByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting product vector by ID: {Id}", id);

                // Redis'te ProductVectorID ile arama
                var searchParams = new object[]
                {
                    _indexName,
                    $"@productVectorId:{id}",
                    "LIMIT", 0, 1
                };

                var result = (RedisResult)await _db.ExecuteAsync("FT.SEARCH", searchParams);
                var searchResults = (RedisResult[])result;

                if (Convert.ToInt64(searchResults[0]) > 0)
                {
                    // İlk sonucun anahtarını al (1. indeks) ve hash verilerini al
                    var key = (string)searchResults[1];
                    var hashEntries = await _db.HashGetAllAsync(key);
                    return ConvertToProductVector(hashEntries);
                }

                // Bulunamazsa daha maliyetli yöntemi dene
                var allVectors = await GetAllAsync();
                return allVectors.FirstOrDefault(v => v.ProductVectorID == id);
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

                var key = $"{_keyPrefix}{productId}";
                var hashEntries = await _db.HashGetAllAsync(key);

                if (hashEntries.Length == 0)
                {
                    _logger.LogInformation("No product vector found for Product ID: {ProductId}", productId);
                    return null;
                }

                return ConvertToProductVector(hashEntries);
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

                var key = $"{_keyPrefix}{productVector.ProductID}";

                // Redis hash olarak vektör bilgilerini sakla
                var hashEntries = new HashEntry[]
                {
                    new HashEntry("productVectorId", productVector.ProductVectorID.ToString()),
                    new HashEntry("productId", productVector.ProductID.ToString()),
                    new HashEntry("text", productVector.Text),
                    new HashEntry("embedding", productVector.Embedding)
                };

                await _db.HashSetAsync(key, hashEntries);
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

                // Redis'te güncelleme, kısmen veya tamamen yeniden yazma şeklinde yapılır
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
                _logger.LogInformation("Saving changes to Redis (no-op in Redis as changes are saved immediately)");

                // Redis verileri anında kaydeder, bu nedenle bu metodda ek bir işlem gerektirmez
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveChangesAsync for Redis");
                throw;
            }
        }

        // Redis'ten gelen hash entrylerini ProductVector nesnesine dönüştürme
        private ProductVector ConvertToProductVector(HashEntry[] hashEntries)
        {
            var dict = hashEntries.ToDictionary(he => he.Name.ToString(), he => he.Value.ToString());

            if (!dict.ContainsKey("productId") || !dict.ContainsKey("text") || !dict.ContainsKey("embedding"))
            {
                _logger.LogWarning("Incomplete product vector data found in Redis");
                return null;
            }

            var productVector = new ProductVector
            {
                ProductID = int.Parse(dict["productId"]),
                Text = dict["text"],
                Embedding = dict["embedding"]
            };

            // ProductVectorID varsa ekle
            if (dict.ContainsKey("productVectorId") && !string.IsNullOrEmpty(dict["productVectorId"]))
            {
                productVector.ProductVectorID = int.Parse(dict["productVectorId"]);
            }

            return productVector;
        }

        // Vector search ile benzer ürünleri bulma metodu
        public List<Tuple<ProductVector, float>> FindSimilarVectors(float[] queryEmbedding, int limit = 5)
        {
            try
            {
                _logger.LogInformation("Finding similar vectors in Redis with KNN search, limit: {Limit}", limit);

                // Embedding'i byte dizisine dönüştür
                var queryVector = ConvertFloatArrayToByteArray(queryEmbedding);

                // KNN sorgusu için parametreler
                var parameters = new List<object>
                {
                    _indexName,
                    $"*=>[KNN {limit} @embedding $query_vector AS score]",
                    "PARAMS", 2, "query_vector", queryVector,
                    "DIALECT", 2,
                    "SORTBY", "score",
                    "LIMIT", 0, limit
                };

                var result = (RedisResult)_db.Execute("FT.SEARCH", parameters.ToArray());
                var searchResults = (RedisResult[])result;

                var totalResults = Convert.ToInt64(searchResults[0]);
                _logger.LogInformation("Found {Count} similar vectors in Redis", totalResults);

                var results = new List<Tuple<ProductVector, float>>();

                // Eğer sonuç varsa işle
                if (totalResults > 0)
                {
                    // Sonuçları işle (indeks 1'den başlar, 2'şer artarak gider - anahtar ve sonra fields)
                    for (int i = 1; i < searchResults.Length; i += 2)
                    {
                        var key = (string)searchResults[i];
                        var fields = (RedisResult[])searchResults[i + 1];

                        // Hash field değerlerini bir dictionary'ye dönüştür
                        var fieldDict = new Dictionary<string, string>();
                        for (int j = 0; j < fields.Length; j += 2)
                        {
                            var fieldName = (string)fields[j];
                            var fieldValue = (string)fields[j + 1];
                            fieldDict[fieldName] = fieldValue;
                        }

                        // ProductVector nesnesini oluştur
                        var productVector = new ProductVector
                        {
                            ProductID = int.Parse(fieldDict["productId"]),
                            Text = fieldDict["text"],
                            Embedding = fieldDict["embedding"]
                        };

                        if (fieldDict.ContainsKey("productVectorId") && !string.IsNullOrEmpty(fieldDict["productVectorId"]))
                        {
                            productVector.ProductVectorID = int.Parse(fieldDict["productVectorId"]);
                        }

                        // Benzerlik skoru
                        float score = 0;
                        if (fieldDict.ContainsKey("score"))
                        {
                            score = float.Parse(fieldDict["score"]);
                            _logger.LogDebug("Product ID: {ProductId}, Similarity Score: {Score}",
                                productVector.ProductID, score);
                        }

                        results.Add(new Tuple<ProductVector, float>(productVector, score));
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar vectors in Redis");
                throw;
            }
        }

        // Float dizisini byte dizisine dönüştürme
        private byte[] ConvertFloatArrayToByteArray(float[] array)
        {
            var byteArray = new byte[array.Length * 4]; // Her float 4 byte
            Buffer.BlockCopy(array, 0, byteArray, 0, byteArray.Length);
            return byteArray;
        }

        // Byte dizisini float dizisine dönüştürme (gerekirse)
        private float[] ConvertByteArrayToFloatArray(byte[] array)
        {
            var floatArray = new float[array.Length / 4];
            Buffer.BlockCopy(array, 0, floatArray, 0, array.Length);
            return floatArray;
        }
    }
}