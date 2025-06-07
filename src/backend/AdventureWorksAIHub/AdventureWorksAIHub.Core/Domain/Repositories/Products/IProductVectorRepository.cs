using AdventureWorksAIHub.Core.Domain.Entities.Products;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Domain.Repositories.Products
{
    public interface IProductVectorRepository
    {
        Task<IEnumerable<ProductVector>> GetAllAsync();
        Task<ProductVector> GetByIdAsync(int id);
        Task<ProductVector> GetByProductIdAsync(int productId);
        Task AddAsync(ProductVector productVector);
        Task UpdateAsync(ProductVector productVector);
        Task SaveChangesAsync();
    }
}
