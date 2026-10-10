using Inventory.Domain.Transactions;
using Inventory.Infrastructure.Persistence.DomainModel;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Infrastructure.Persistence.Repositories
{
    internal class TransactionRepository : ITransactionRepository
    {
        private readonly DomainDbContext _dbContext;

        public TransactionRepository(DomainDbContext domainDbContext)
        {
            _dbContext = domainDbContext;
        }

        public async Task AddAsync(Transaction obj)
        {
            await _dbContext.Transaction.AddAsync(obj);
        }

        public async Task DeleteAsync(Guid id)
        {
            var obj = await GetByIdAsync(id);
            if(obj != null)
                _dbContext.Transaction.Remove(obj);
        }

        public async Task<Transaction?> GetByIdAsync(Guid id, bool readOnly = false)
        {
            if (readOnly)
            {
                return await _dbContext.Transaction.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            }
            else
            {
                return await _dbContext.Transaction.Include("_items").SingleOrDefaultAsync(i => i.Id == id);
            }
        }

        public Task<Transaction?> GetByInvoiceAsync(string invoiceNumber)
        {
            return _dbContext.Transaction.Include("_items")
                .FirstOrDefaultAsync(t => t.InvoiceNumber == invoiceNumber);
        }

        public async Task<(IReadOnlyList<Transaction> Transactions, int Total)> GetPendingExitsAsync(int skip, int take)
        {
            IQueryable<Transaction> pending = _dbContext.Transaction.AsNoTracking()
                .Where(t => t.Type == TransactionType.Exit && t.Status == TransactionStatus.Created && t.SourceId != null);

            int total = await pending.CountAsync();
            List<Transaction> page = await pending.Include("_items").OrderBy(t => t.CreationDate).Skip(skip).Take(take).ToListAsync();

            return (page, total);
        }

        public Task<Transaction?> GetBySourceAsync(Guid sourceId, string sourceType)
        {
            return _dbContext.Transaction.Include("_items")
                .SingleOrDefaultAsync(t => t.SourceId == sourceId && t.SourceType == sourceType);
        }

        public Task UpdateAsync(Transaction transaction)
        {
            _dbContext.Transaction.Update(transaction);
            return Task.CompletedTask;
        }
    }
}
