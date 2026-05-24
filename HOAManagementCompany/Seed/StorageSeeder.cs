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
            (Name: "2026 HOA Budget", Category: DocumentCategory.Budgets, Pinned: true, Key: "documents/2026/budget-2026.pdf"),
            (Name: "Community Rules & Regulations", Category: DocumentCategory.Rules, Pinned: true, Key: "documents/rules/rules-2024.pdf"),
            (Name: "CC&R Declaration", Category: DocumentCategory.Governing, Pinned: false, Key: "documents/governing/ccr-declaration.pdf"),
            (Name: "Architectural Review Form", Category: DocumentCategory.Forms, Pinned: false, Key: "documents/forms/arch-review-form.pdf"),
            (Name: "May 2026 Board Meeting Minutes", Category: DocumentCategory.Minutes, Pinned: false, Key: "documents/minutes/2026-05-board-minutes.pdf")
        };

        foreach (var (name, category, pinned, key) in documents)
        {
            try
            {
                await storage.UploadAsync(key, $"Placeholder content for: {name}", ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to upload placeholder for {Name} — continuing", name);
            }

            db.HoaDocuments.Add(new HoaDocument
            {
                CommunityId = communityId,
                Name = name,
                Category = category,
                EffectiveDate = DateOnly.FromDateTime(DateTime.Today),
                FileSizeLabel = "1.2 MB",
                Pinned = pinned,
                StorageKey = key
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("StorageSeeder complete.");
    }
}
