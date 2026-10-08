namespace Inventory.Infrastructure.Persistence;

public interface IDatabase : IDisposable
{
    void Migrate();
}
