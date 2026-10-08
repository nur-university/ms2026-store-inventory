using Inventory.Infrastructure.Inbox;
using Inventory.Infrastructure.Persistence.StoredModel.Entities;
using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.EFCore.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Infrastructure.Persistence.StoredModel
{
    internal class StoredDbContext : DbContext, IDatabase
    {
        public DbSet<ItemStoredModel> Item { get; set; }
        public DbSet<TransactionStoredModel> Transaction { get; set; }
        public DbSet<TransactionItemStoredModel> TransactionItem { get; set; }
        public DbSet<UserStoredModel> User { get; set; }

        public StoredDbContext(DbContextOptions<StoredDbContext> options) : base(options)
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.AddOutboxModel<DomainEvent>();
            modelBuilder.AddInboxModel();

            // One reservation per originating order (exit transactions created by hand have no source).
            modelBuilder.Entity<TransactionStoredModel>()
                .HasIndex(t => new { t.SourceId, t.SourceType })
                .IsUnique()
                .HasFilter("\"sourceId\" IS NOT NULL");
        }

        public void Migrate()
        {
            Database.Migrate();
        }
    }
}
