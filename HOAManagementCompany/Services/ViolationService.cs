using HOAManagementCompany.Models;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Services
{
    public class ViolationService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        public ViolationService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
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
            using var context = _dbContextFactory.CreateDbContext();
            
            // Get the existing violation from the database
            var existingViolation = await context.Violations.FindAsync(violation.Id);
            if (existingViolation == null)
            {
                throw new InvalidOperationException($"Violation with ID {violation.Id} not found.");
            }
            
            // Update the properties
            existingViolation.Description = violation.Description;
            existingViolation.Status = violation.Status;
            existingViolation.OccurrenceDate = violation.OccurrenceDate;
            existingViolation.ViolationTypeId = violation.ViolationTypeId;
            
            // Save changes
            await context.SaveChangesAsync();
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
    }
}
