using HOAManagementCompany.Models;
using Microsoft.EntityFrameworkCore;

namespace HOAManagementCompany.Services
{
    public class ViolationService
    {
        private readonly ApplicationDbContext _context;

        public ViolationService(ApplicationDbContext context)
        {
            _context = context;
        }
        
        // ViolationType methods
        // NEW METHOD: To get a single Violation Type by Id (useful for edit pages)
        public async Task<ViolationType?> GetViolationTypeByIdAsync(Guid id)
        {
            return await _context.ViolationTypes.FindAsync(id);
        }
        public async Task<List<ViolationType>> GetViolationTypesAsync()
        {
            return await _context.ViolationTypes.OrderBy(vt => vt.Name).ToListAsync();
        }
        public async Task AddViolationTypeAsync(ViolationType violationType)
        {
            _context.ViolationTypes.Add(violationType);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateViolationTypeAsync(ViolationType violationType)
        {
            _context.ViolationTypes.Update(violationType);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteViolationTypeAsync(Guid id)
        {
            var violationType = await _context.ViolationTypes.FindAsync(id);
            if (violationType != null)
            {
                _context.ViolationTypes.Remove(violationType);
                await _context.SaveChangesAsync();
            }
        }
        
        // Violation methods
        public async Task<Violation?> GetViolationByIdAsync(Guid id)
        {
            return await _context.Violations
                .Include(v => v.ViolationType)
                .FirstOrDefaultAsync(v => v.Id == id);
        }
        
        public async Task<List<Violation>> GetViolationsAsync()
        {
            return await _context.Violations
                .Include(v => v.ViolationType)
                .OrderByDescending(v => v.OccurrenceDate)
                .ToListAsync();
        }
        
        public async Task AddViolationAsync(Violation violation)
        {
            _context.Violations.Add(violation);
            await _context.SaveChangesAsync();
        }
        
        public async Task UpdateViolationAsync(Violation violation)
        {
            var existingViolation = await _context.Violations.FindAsync(violation.Id);
            if (existingViolation != null)
            {
                existingViolation.Description = violation.Description;
                existingViolation.Status = violation.Status;
                existingViolation.OccurrenceDate = violation.OccurrenceDate;
                existingViolation.ViolationTypeId = violation.ViolationTypeId;
                await _context.SaveChangesAsync();
            }
        }
        
        public async Task DeleteViolationAsync(Guid id)
        {
            var violation = await _context.Violations.FindAsync(id);
            if (violation != null)
            {
                _context.Violations.Remove(violation);
                await _context.SaveChangesAsync();
            }
        }


    }
}
