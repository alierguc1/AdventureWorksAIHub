using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Application.Services
{
    public interface IOllamaService
    {
        Task<string> GenerateCompletionAsync(string prompt, float temperature = 0.7f);
        Task<float[]> EmbedTextAsync(string text);
    }
}
