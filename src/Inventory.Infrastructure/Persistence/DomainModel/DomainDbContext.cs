using Inventory.Infrastructure.Inbox;
using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Inventory.Domain.Transactions.Events;
using Inventory.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.EFCore.Persistence;
using Joseco.Outbox.Contracts.Model;

namespace Inventory.Infrastructure.Persistence.DomainModel
{
    internal class DomainDbContext : DbContext
    {
        public DbSet<Item> Item { get; set; }
        public DbSet<Transaction> Transaction { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<OutboxMessage<DomainEvent>> OutboxMessages { get; set; }
    public DbSet<InboxMessage> InboxMessages { get; set; }

        public DomainDbContext(DbContextOptions<DomainDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomainDbContext).Assembly);
            modelBuilder.AddOutboxModel<DomainEvent>();
            modelBuilder.AddInboxModel();

            base.OnModelCreating(modelBuilder);

            modelBuilder.Ignore<DomainEvent>();

            if (Database.IsNpgsql())
            {
                // Optimistic concurrency: two workers changing the same row at once must not overwrite each other.
                modelBuilder.Entity<Item>().Property<uint>("xmin")
                    .HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            }
            modelBuilder.Ignore<TransactionCompleted>();
        }
    }
}
