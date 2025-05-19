using AdventureWorksAIHub.Core.Domain.Entities;
using AdventureWorksAIHub.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Infrastructure.Persisitence.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly AdventureWorksContext _context;

        public ProductRepository(AdventureWorksContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _context.Products.ToListAsync();
        }

        public async Task<Product> GetByIdAsync(int id)
        {
            return await _context.Products.FindAsync(id);
        }

        public async Task<IEnumerable<Product>> GetProductsWithDescriptionsAsync()
        {
            return await _context.Products
                .Include(p => p.ProductDescription)
                .ToListAsync();
        }

        public async Task<Product> GetProductWithDescriptionAsync(int id)
        {
            return await _context.Products
                .Include(p => p.ProductDescription)
                .FirstOrDefaultAsync(p => p.ProductID == id);
        }

        public async Task<IEnumerable<Product>> GetProductsByIdsAsync(List<int> productIds)
        {
            return await _context.Products
                .Include(p => p.ProductDescription)
                .Where(p => productIds.Contains(p.ProductID))
                .ToListAsync();
        }
    }
}
