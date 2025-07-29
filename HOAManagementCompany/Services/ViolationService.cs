using HOAManagementCompany.Models;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Services
{
    public class ViolationService : BaseService
    {
        public ViolationService(IDbContextFactory<ApplicationDbContext> dbContextFactory) 
            : base(dbContextFactory)
        {
        }
        
        // ViolationType methods
        // NEW METHOD: To get a single Violation Type by Id (useful for edit pages)
        public async Task<ViolationType?> GetViolationTypeByIdAsync(Guid id)
        {
            using var context = _dbContextFactory.CreateDbContext();
            return await context.ViolationTypes.FindAsync(id);
        }
        public async Task<List<ViolationType>> GetViolationTypesAsync()
        {
            using var context = _dbContextFactory.CreateDbContext();
            return await context.ViolationTypes.OrderBy(vt => vt.Name).ToListAsync();
        }
        public async Task AddViolationTypeAsync(ViolationType violationType)
        {
            using var context = _dbContextFactory.CreateDbContext();
            context.ViolationTypes.Add(violationType);
            await context.SaveChangesAsync();
        }
        public async Task UpdateViolationTypeAsync(ViolationType violationType)
        {
            using var context = _dbContextFactory.CreateDbContext();
            context.ViolationTypes.Update(violationType);
            await context.SaveChangesAsync();
        }

        public async Task DeleteViolationTypeAsync(Guid id)
        {
            using var context = _dbContextFactory.CreateDbContext();
            var violationType = await context.ViolationTypes.FindAsync(id);
            if (violationType != null)
            {
                context.ViolationTypes.Remove(violationType);
                await context.SaveChangesAsync();
            }
        }
        
        // Violation methods
        public async Task<Violation?> GetViolationByIdAsync(Guid id)
        {
            using var context = _dbContextFactory.CreateDbContext();
            return await context.Violations
                .Include(v => v.ViolationType)
                .FirstOrDefaultAsync(v => v.Id == id);
        }
        
        public async Task<List<Violation>> GetViolationsAsync()
        {
            using var context = _dbContextFactory.CreateDbContext();
            return await context.Violations
                .Include(v => v.ViolationType)
                .OrderByDescending(v => v.OccurrenceDate)
                .ToListAsync();
        }
        
        public async Task AddViolationAsync(Violation violation)
        {
            using var context = _dbContextFactory.CreateDbContext();
            context.Violations.Add(violation);
            await context.SaveChangesAsync();
        }
        
        public async Task UpdateViolationAsync(Violation violation)
        {
            await SafeUpdateAsync(violation, existingViolation =>
            {
                existingViolation.Description = violation.Description;
                existingViolation.Status = violation.Status;
                existingViolation.OccurrenceDate = violation.OccurrenceDate;
                existingViolation.ViolationTypeId = violation.ViolationTypeId;
            });
        }
        
        public async Task DeleteViolationAsync(Guid id)
        {
            using var context = _dbContextFactory.CreateDbContext();
            var violation = await context.Violations.FindAsync(id);
            if (violation != null)
            {
                context.Violations.Remove(violation);
                await context.SaveChangesAsync();
            }
        }

        protected override object GetEntityId<T>(T entity) where T : class
        {
            return entity switch
            {
                Violation violation => violation.Id,
                ViolationType violationType => violationType.Id,
                _ => throw new ArgumentException($"Unsupported entity type: {typeof(T).Name}")
            };
        }
    }
}
