using AdventureWorksAIHub.Core.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Domain.Repositories
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product> GetByIdAsync(int id);
        Task<IEnumerable<Product>> GetProductsWithDescriptionsAsync();
        Task<Product> GetProductWithDescriptionAsync(int id);
        Task<IEnumerable<Product>> GetProductsByIdsAsync(List<int> productIds);
    }
}
