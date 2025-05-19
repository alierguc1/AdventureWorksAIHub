using AdventureWorksAIHub.Core.Application.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Infrastructure.Services
{
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;

        public OllamaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _baseUrl = configuration["OllamaSettings:BaseUrl"];
            _model = configuration["OllamaSettings:Model"];
        }

        public async Task<string> GenerateCompletionAsync(string prompt, float temperature = 0.7f)
        {
            var requestData = new
            {
                model = _model,
                prompt = prompt,
                temperature = temperature,
                stream = false
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<OllamaResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return responseObject?.Response ?? string.Empty;
        }

        public async Task<float[]> EmbedTextAsync(string text)
        {
            var requestData = new
            {
                model = _model,
                prompt = text,
                options = new { embedding = true }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/embeddings", content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return responseObject?.Embedding ?? Array.Empty<float>();
        }

        private class OllamaResponse
        {
            public string Response { get; set; }
        }

        private class OllamaEmbeddingResponse
        {
            public float[] Embedding { get; set; }
        }
    }
}
