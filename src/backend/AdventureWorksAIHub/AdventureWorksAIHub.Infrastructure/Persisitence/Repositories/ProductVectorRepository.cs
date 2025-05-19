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
    public class ProductVectorRepository : IProductVectorRepository
    {
        private readonly AdventureWorksContext _context;

        public ProductVectorRepository(AdventureWorksContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProductVector>> GetAllAsync()
        {
            return await _context.ProductVectors.ToListAsync();
        }

        public async Task<ProductVector> GetByIdAsync(int id)
        {
            return await _context.ProductVectors.FindAsync(id);
        }

        public async Task<ProductVector> GetByProductIdAsync(int productId)
        {
            return await _context.ProductVectors
                .FirstOrDefaultAsync(pv => pv.ProductID == productId);
        }

        public async Task AddAsync(ProductVector productVector)
        {
            await _context.ProductVectors.AddAsync(productVector);
        }

        public Task UpdateAsync(ProductVector productVector)
        {
            _context.ProductVectors.Update(productVector);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}