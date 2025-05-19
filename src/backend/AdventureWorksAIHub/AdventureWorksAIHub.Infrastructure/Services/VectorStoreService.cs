using AdventureWorksAIHub.Core.Application.Dtos;
using AdventureWorksAIHub.Core.Application.Services;
using AdventureWorksAIHub.Core.Domain.Entities;
using AdventureWorksAIHub.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;
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
        private readonly IProductVectorRepository _productVectorRepository;
        private readonly IOllamaService _ollamaService;
        private readonly ILogger<VectorStoreService> _logger;

        public VectorStoreService(
            IProductRepository productRepository,
            IProductVectorRepository productVectorRepository,
            IOllamaService ollamaService,
            ILogger<VectorStoreService> logger)
        {
            _productRepository = productRepository;
            _productVectorRepository = productVectorRepository;
            _ollamaService = ollamaService;
            _logger = logger;
        }

        public async Task IndexProductDescriptionsAsync()
        {
            _logger.LogInformation("Starting product descriptions indexing");

            var products = await _productRepository.GetProductsWithDescriptionsAsync();
            int count = 0;

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

                    var existingVector = await _productVectorRepository.GetByProductIdAsync(product.ProductID);
                    if (existingVector != null)
                    {
                        existingVector.Text = text;
                        existingVector.Embedding = JsonSerializer.Serialize(embedding);
                        await _productVectorRepository.UpdateAsync(existingVector);
                    }
                    else
                    {
                        var vectorRecord = new ProductVector
                        {
                            ProductID = product.ProductID,
                            Text = text,
                            Embedding = JsonSerializer.Serialize(embedding)
                        };
                        await _productVectorRepository.AddAsync(vectorRecord);
                    }

                    count++;

                    if (count % 10 == 0)
                    {
                        await _productVectorRepository.SaveChangesAsync();
                        _logger.LogInformation($"Indexed {count} products so far");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error indexing product {product.ProductID}");
                }
            }

            await _productVectorRepository.SaveChangesAsync();
            _logger.LogInformation($"Completed indexing {count} products");
        }

        public async Task<List<SearchResultDto>> FindSimilarProductsAsync(string query, int limit = 5)
        {
            var queryEmbedding = await _ollamaService.EmbedTextAsync(query);
            var allVectors = await _productVectorRepository.GetAllAsync();
            var results = new List<SearchResultDto>();

            foreach (var vector in allVectors)
            {
                try
                {
                    var productVector = JsonSerializer.Deserialize<float[]>(vector.Embedding);
                    var similarity = CosineSimilarity(queryEmbedding, productVector);

                    results.Add(new SearchResultDto
                    {
                        ProductID = vector.ProductID,
                        Text = vector.Text,
                        Similarity = similarity
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error calculating similarity for product {vector.ProductID}");
                }
            }

            return results
                .OrderByDescending(r => r.Similarity)
                .Take(limit)
                .ToList();
        }

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