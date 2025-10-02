using ASI.Basecode.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data
{

    /// <summary>
    /// Unit of Work Implementation
    /// </summary>
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        /// <summary>
        /// Gets the database context
        /// </summary>
        public DbContext Database { get; private set; }

        /// <summary>
        /// Initializes a new instance of the UnitOfWork class.
        /// </summary>
        /// <param name="serviceContext">The service context.</param>
        public UnitOfWork(AsiBasecodeDBContext serviceContext)
        {
            Database = serviceContext;
        }

        /// <summary>
        /// Saves the changes to database
        /// </summary>
        public void SaveChanges()
        {
            Database.SaveChanges();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Database.Dispose();
        }

        // added for transaction support
        public IDbContextTransaction? CurrentTransaction { get; private set; }
        public bool HasActiveTransaction => CurrentTransaction != null;

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => Database.SaveChangesAsync(ct);

        public async Task BeginTransactionAsync(CancellationToken ct = default)
        {
            if (CurrentTransaction != null) return;
            CurrentTransaction = await Database.Database.BeginTransactionAsync(ct);
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (CurrentTransaction == null) return;
            await CurrentTransaction.CommitAsync(ct);
            await CurrentTransaction.DisposeAsync();
            CurrentTransaction = null;
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (CurrentTransaction == null) return;
            await CurrentTransaction.RollbackAsync(ct);
            await CurrentTransaction.DisposeAsync();
            CurrentTransaction = null;
        }

    }
}
