using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdventureWorksAIHub.Core.Domain.Entities.Products;

namespace AdventureWorksAIHub.Core.Domain.Repositories.Products
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
