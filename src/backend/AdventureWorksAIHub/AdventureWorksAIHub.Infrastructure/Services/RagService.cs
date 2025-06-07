using AdventureWorksAIHub.Core.Application.Dtos;
using AdventureWorksAIHub.Core.Application.Services;
using AdventureWorksAIHub.Core.Domain.Repositories;
using AutoMapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Infrastructure.Services
{
    public class RagService : IRagService
    {
        private readonly IProductRepository _productRepository;
        private readonly IVectorStoreService _vectorStoreService;
        private readonly IOllamaService _ollamaService;
        private readonly IMapper _mapper;
        private readonly ILogger<RagService> _logger;
        public RagService(
            IProductRepository productRepository,
            IVectorStoreService vectorStoreService,
            IOllamaService ollamaService,
            IMapper mapper,
            ILogger<RagService> logger)
        {
            _productRepository = productRepository;
            _vectorStoreService = vectorStoreService;
            _ollamaService = ollamaService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<RagResponseDto> AskQuestionAsync(string question)
        {
            _logger.LogInformation($"Processing question: {question}");

            var similarProducts = await _vectorStoreService.FindSimilarProductsAsync(question, 3);
            if (similarProducts.Count == 0)
            {
                _logger.LogInformation("No similar products found");

                var directResponse = await _ollamaService.GenerateCompletionAsync(question);
                return new RagResponseDto { Answer = directResponse };
            }

            var productIds = similarProducts.Select(p => p.ProductID).ToList();
            var products = await _productRepository.GetProductsByIdsAsync(productIds);

            // Manuel eşleştirme kullanın
            var relatedProducts = products.Select(p => new ProductInfoDto
            {
                ProductID = p.ProductID,
                Name = p.Name ?? string.Empty,
                Price = p.ListPrice
            }).ToList();

            var context = string.Join("\n\n", products.Select(p =>
                $"Product: {p.Name}\n" +
                $"Product Number: {p.ProductNumber}\n" +
                $"Category: {p.ProductSubcategoryID}\n" +
                $"Price: ${p.ListPrice}\n" +
                $"Description: {p.ProductDescription?.Description ?? "No description available"}"
            ));

            var enhancedPrompt = $@"
            Answer the following question based on the product information provided:
            {context}
            Question: {question}
            Answer:";

            var answer = await _ollamaService.GenerateCompletionAsync(enhancedPrompt, 0.7f);

            return new RagResponseDto
            {
                Answer = answer,
                RelatedProducts = relatedProducts
            };
        }
    }
}