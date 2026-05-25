using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Infrastructure.Persistence;
using HOAManagementCompany.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace HOAManagementCompany.Seed;

public class StorageSeeder(ApplicationDbContext db, IServiceProvider services, SeedResult result, ILogger logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var storage = services.GetRequiredService<IDocumentStorage>();
        var communityId = result.CommunityId;

        var documents = new[]
        {
            // Pinned / essential docs
            ("2026 HOA Operating Budget",              DocumentCategory.Budgets,   true,  "documents/2026/budget-2026.pdf",                 "2026-01-01", 1_248_000L),
            ("Community Rules & Regulations",          DocumentCategory.Rules,     true,  "documents/rules/rules-2024.pdf",                 "2024-03-15", 3_145_728L),
            ("CC&R Declaration",                       DocumentCategory.Governing, true,  "documents/governing/ccr-declaration.pdf",        "2005-06-01", 5_242_880L),

            // Forms
            ("Architectural Review Form",              DocumentCategory.Forms,     false, "documents/forms/arch-review-form.pdf",           "2025-01-01", 512_000L),
            ("Move-In / Move-Out Request Form",        DocumentCategory.Forms,     false, "documents/forms/move-in-out-form.pdf",           "2025-01-01", 384_000L),
            ("Pet Registration Form",                  DocumentCategory.Forms,     false, "documents/forms/pet-registration.pdf",           "2024-09-01", 204_800L),
            ("Amenity Reservation Form",               DocumentCategory.Forms,     false, "documents/forms/amenity-reservation.pdf",        "2024-09-01", 256_000L),

            // Board Minutes
            ("May 2026 Board Meeting Minutes",         DocumentCategory.Minutes,   false, "documents/minutes/2026-05-board-minutes.pdf",    "2026-05-13", 624_000L),
            ("April 2026 Board Meeting Minutes",       DocumentCategory.Minutes,   false, "documents/minutes/2026-04-board-minutes.pdf",    "2026-04-08", 589_000L),
            ("March 2026 Board Meeting Minutes",       DocumentCategory.Minutes,   false, "documents/minutes/2026-03-board-minutes.pdf",    "2026-03-11", 612_000L),
            ("2025 Annual Meeting Minutes",            DocumentCategory.Minutes,   false, "documents/minutes/2025-annual-minutes.pdf",      "2025-11-18", 1_024_000L),

            // Budgets & Financials
            ("2025 Annual Financial Statement",        DocumentCategory.Financials, false, "documents/financials/2025-annual-statement.pdf","2026-02-28", 2_097_152L),
            ("Q1 2026 Financial Report",               DocumentCategory.Financials, false, "documents/financials/2026-q1-report.pdf",       "2026-04-15", 876_000L),
            ("Reserve Fund Study 2024–2034",           DocumentCategory.Financials, false, "documents/financials/reserve-study-2024.pdf",   "2024-07-01", 4_194_304L),

            // Insurance
            ("Master Insurance Policy 2026",           DocumentCategory.Insurance, false, "documents/insurance/master-policy-2026.pdf",    "2026-01-01", 1_572_864L),
            ("Certificate of Liability Insurance",     DocumentCategory.Insurance, false, "documents/insurance/cert-liability-2026.pdf",   "2026-01-01", 409_600L),

            // Governing
            ("HOA Bylaws",                             DocumentCategory.Governing, false, "documents/governing/bylaws.pdf",               "2005-06-01", 2_621_440L),
            ("Articles of Incorporation",              DocumentCategory.Governing, false, "documents/governing/articles-of-inc.pdf",      "2005-01-12", 819_200L),
        };

        foreach (var (name, category, pinned, key, effectiveDateStr, sizeBytes) in documents)
        {
            // Build a minimal but realistic-looking PDF byte sequence
            var placeholder = $"%PDF-1.4\n%Placeholder for: {name}\n%%EOF";
            try
            {
                await storage.UploadAsync(key, placeholder, ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to upload placeholder for {Name} — continuing", name);
            }

            var effectiveDate = DateOnly.Parse(effectiveDateStr);
            var sizeLabel = sizeBytes >= 1_048_576
                ? $"{sizeBytes / 1_048_576.0:F1} MB"
                : $"{sizeBytes / 1024} KB";

            db.HoaDocuments.Add(new HoaDocument
            {
                CommunityId   = communityId,
                Name          = name,
                Category      = category,
                EffectiveDate = effectiveDate,
                FileSizeLabel = sizeLabel,
                Pinned        = pinned,
                StorageKey    = key
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("StorageSeeder complete.");
    }
}
