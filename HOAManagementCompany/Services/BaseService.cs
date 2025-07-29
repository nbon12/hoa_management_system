using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Services
{
    public abstract class BaseService
    {
        protected readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        protected BaseService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        /// <summary>
        /// Safely updates an entity by fetching it from the database first.
        /// This prevents EF change tracking issues with navigation properties.
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="entity">The entity with updated values</param>
        /// <param name="updateAction">Action to apply updates to the fetched entity</param>
        /// <returns>Task</returns>
        protected async Task SafeUpdateAsync<T>(T entity, Action<T> updateAction) where T : class
        {
            using var context = _dbContextFactory.CreateDbContext();
            
            // Get the existing entity from the database
            var existingEntity = await context.Set<T>().FindAsync(GetEntityId(entity));
            if (existingEntity == null)
            {
                throw new InvalidOperationException($"Entity of type {typeof(T).Name} not found.");
            }
            
            // Apply updates using the provided action
            updateAction(existingEntity);
            
            // Save changes
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Gets the primary key value from an entity.
        /// Override this in derived classes for specific entity types.
        /// </summary>
        protected abstract object GetEntityId<T>(T entity) where T : class;
    }
} 