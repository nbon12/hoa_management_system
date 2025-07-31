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
            return await context.ViolationTypes.FirstOrDefaultAsync(vt => vt.Id == id);
        }
        
        // NEW METHOD: To get a single Violation Type by Id including soft deleted ones (for audit purposes)
        public async Task<ViolationType?> GetViolationTypeByIdIncludingDeletedAsync(Guid id)
        {
            using var context = _dbContextFactory.CreateDbContext();
            return await context.ViolationTypes.IgnoreQueryFilters().FirstOrDefaultAsync(vt => vt.Id == id);
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
        
        public async Task<PagedList<Violation>> GetViolationsPagedAsync(
            PaginationParameters paginationParams,
            ViolationStatus? status = null,
            Guid? violationTypeId = null,
            string? testNamespace = null)
        {
            using var context = _dbContextFactory.CreateDbContext();
            
            IQueryable<Violation> query = context.Violations.Include(v => v.ViolationType);

            // Apply filters
            if (status.HasValue)
            {
                query = query.Where(v => v.Status == status.Value);
            }

            if (violationTypeId.HasValue)
            {
                query = query.Where(v => v.ViolationTypeId == violationTypeId.Value);
            }

            // Apply test namespace filter if provided (for testing isolation)
            if (!string.IsNullOrWhiteSpace(testNamespace))
            {
                query = query.Where(v => v.Description.Contains(testNamespace));
            }

            // Apply search term if provided
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var searchTerm = paginationParams.SearchTerm.ToLower();
                query = query.Where(v => v.Description.ToLower().Contains(searchTerm));
            }

            // Apply ordering
            if (!string.IsNullOrWhiteSpace(paginationParams.OrderBy))
            {
                query = paginationParams.OrderBy.ToLower() switch
                {
                    "description" => paginationParams.OrderDesc 
                        ? query.OrderByDescending(v => v.Description)
                        : query.OrderBy(v => v.Description),
                    "status" => paginationParams.OrderDesc 
                        ? query.OrderByDescending(v => v.Status)
                        : query.OrderBy(v => v.Status),
                    "occurrencedate" => paginationParams.OrderDesc 
                        ? query.OrderByDescending(v => v.OccurrenceDate)
                        : query.OrderBy(v => v.OccurrenceDate),
                    "violationtype" => paginationParams.OrderDesc 
                        ? query.OrderByDescending(v => v.ViolationType.Name)
                        : query.OrderBy(v => v.ViolationType.Name),
                    _ => query.OrderByDescending(v => v.OccurrenceDate) // Default ordering
                };
            }
            else
            {
                // Default ordering is crucial for consistent pagination
                query = query.OrderByDescending(v => v.OccurrenceDate);
            }

            return await PagedList<Violation>.CreateAsync(query, paginationParams.PageNumber, paginationParams.PageSize);
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
