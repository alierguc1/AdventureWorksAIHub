using AdventureWorksAIHub.Core.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Application.Services
{
    public interface IVectorStoreService
    {
        Task IndexProductDescriptionsAsync();
        Task<List<SearchResultDto>> FindSimilarProductsAsync(string query, int limit = 5);
    }
}
