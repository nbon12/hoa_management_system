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
    }
}
