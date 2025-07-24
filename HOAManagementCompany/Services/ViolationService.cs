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
    }
}
