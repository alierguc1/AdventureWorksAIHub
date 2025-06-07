using AdventureWorksAIHub.Core.Application.Dtos;
using AdventureWorksAIHub.Core.Application.Services;
using AdventureWorksAIHub.Core.Domain.Entities;
using AdventureWorksAIHub.Core.Domain.Repositories.Product;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Operation.Distance;
using Qdrant.Client;
using Qdrant.Client.Grpc;
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
        private readonly QdrantClient _qdrantClient;
        private readonly IOllamaService _ollamaService;
        private readonly ILogger<VectorStoreService> _logger;
        private readonly string _collectionName;
        private readonly int _batchSize;

        public VectorStoreService(
            IProductRepository productRepository,
            QdrantClient qdrantClient,
            IOllamaService ollamaService,
            ILogger<VectorStoreService> logger,
            IConfiguration configuration)
        {
            _productRepository = productRepository;
            _qdrantClient = qdrantClient;
            _ollamaService = ollamaService;
            _logger = logger;

            _collectionName = configuration.GetValue<string>("VectorStore:CollectionName") ?? "product_vectors";
            _batchSize = configuration.GetValue<int>("VectorStore:BatchSize", 50);

            _logger.LogInformation("VectorStoreService initialized. Collection: {CollectionName}", _collectionName);

            // Collection'ı başlangıçta kontrol et
            InitializeCollectionAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeCollectionAsync()
        {
            try
            {
                var collections = await _qdrantClient.ListCollectionsAsync();
                var collectionExists = collections.Contains(_collectionName);

                if (!collectionExists)
                {
                    _logger.LogInformation("Collection {CollectionName} does not exist. It will be created during first indexing.", _collectionName);
                }
                else
                {
                    _logger.LogInformation("Collection {CollectionName} already exists.", _collectionName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking collection: {CollectionName}", _collectionName);
            }
        }

        public async Task IndexProductDescriptionsAsync()
        {
            _logger.LogInformation("Starting product descriptions indexing in Qdrant");

            try
            {
                var products = await _productRepository.GetProductsWithDescriptionsAsync();
                int count = 0;
                int total = products.Count();

                _logger.LogInformation("Found {Total} products to index", total);

                if (total == 0)
                {
                    _logger.LogWarning("No products found to index");
                    return;
                }

                // Collection var mı kontrol et, yoksa oluştur
                await EnsureCollectionExistsAsync();

                var points = new List<PointStruct>();

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

                        var point = new PointStruct
                        {
                            Id = (ulong)product.ProductID,
                            Vectors = embedding,
                            Payload =
                            {
                                ["productId"] = product.ProductID,
                                ["productName"] = product.Name ?? "",
                                ["text"] = text,
                                ["description"] = product.ProductDescription.Description ?? ""
                            }
                        };

                        points.Add(point);
                        count++;

                        if (points.Count >= _batchSize)
                        {
                            await _qdrantClient.UpsertAsync(_collectionName, points);
                            _logger.LogInformation("Indexed {Count}/{Total} products ({Percentage:F1}%)",
                                count, total, (count * 100.0 / total));
                            points.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error indexing product {ProductId}: {ProductName}",
                            product.ProductID, product.Name);
                    }
                }

                // Kalan pointleri kaydet
                if (points.Count > 0)
                {
                    await _qdrantClient.UpsertAsync(_collectionName, points);
                }

                _logger.LogInformation("Completed indexing {Count}/{Total} products in Qdrant", count, total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during product indexing");
                throw;
            }
        }

        private async Task EnsureCollectionExistsAsync()
        {
            try
            {
                var collections = await _qdrantClient.ListCollectionsAsync();
                if (!collections.Contains(_collectionName))
                {
                    // İlk embedding'i al ve boyutunu öğren
                    var testEmbedding = await _ollamaService.EmbedTextAsync("test");
                    var vectorSize = testEmbedding.Length;

                    _logger.LogInformation("Creating collection {CollectionName} with dimension {VectorSize}",
                        _collectionName, vectorSize);

                    await _qdrantClient.CreateCollectionAsync(
                        collectionName: _collectionName,
                        vectorsConfig: new VectorParams
                        {
                            Size = (ulong)vectorSize,
                            Distance = Distance.Cosine
                        });

                    _logger.LogInformation("Successfully created collection {CollectionName}", _collectionName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring collection exists");
                throw;
            }
        }

        public async Task<List<SearchResultDto>> FindSimilarProductsAsync(string query, int limit = 5)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<SearchResultDto>();
            }

            _logger.LogInformation("Finding products similar to: '{Query}', limit: {Limit}", query, limit);

            try
            {
                var queryEmbedding = await _ollamaService.EmbedTextAsync(query);
                _logger.LogDebug("Generated embedding for query with dimension {Dimension}", queryEmbedding.Length);

                var searchResults = await _qdrantClient.SearchAsync(
                    collectionName: _collectionName,
                    vector: queryEmbedding,
                    limit: (ulong)limit,
                    payloadSelector: true,
                    scoreThreshold: 0.1f
                );

                var results = new List<SearchResultDto>();

                foreach (var result in searchResults)
                {
                    try
                    {
                        var searchResult = new SearchResultDto
                        {
                            ProductID = (int)(long)result.Payload["productId"].IntegerValue,
                            Text = result.Payload["text"].StringValue,
                            Similarity = result.Score
                        };
                        results.Add(searchResult);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing search result");
                    }
                }

                _logger.LogInformation("Found {Count} similar products", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar products: {Error}", ex.Message);
                return new List<SearchResultDto>();
            }
        }

        public async Task<int> GetVectorCountAsync()
        {
            try
            {
                var collections = await _qdrantClient.ListCollectionsAsync();
                if (!collections.Contains(_collectionName))
                {
                    return 0;
                }

                var collectionInfo = await _qdrantClient.GetCollectionInfoAsync(_collectionName);
                return (int)collectionInfo.VectorsCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vector count");
                return 0;
            }
        }

        public async Task ClearAllVectorsAsync()
        {
            try
            {
                _logger.LogInformation("Clearing all vectors from collection: {CollectionName}", _collectionName);

                var collections = await _qdrantClient.ListCollectionsAsync();
                if (collections.Contains(_collectionName))
                {
                    await _qdrantClient.DeleteCollectionAsync(_collectionName);
                    _logger.LogInformation("Collection {CollectionName} deleted", _collectionName);
                }

                _logger.LogInformation("All vectors cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing vectors");
                throw;
            }
        }
    }
}