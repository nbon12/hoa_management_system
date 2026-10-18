using System.Net;
using System.Net.Http.Json;
using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Auth;
using HOAManagementCompany.Infrastructure.Storage;
using HOAManagementCompany.Tests.Fixtures;
using HOAManagementCompany.Tests.Integration.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HOAManagementCompany.Tests.Integration.Residents;

/// <summary>
/// 030 T099 — a resident's home in an Archived or Onboarding community, with real data behind
/// it: the cases beyond <see cref="ResidentAccessTheoryTests"/> (FR-012a, R8).
/// </summary>
public class ArchivedResidentHomeTests(TestDatabaseFixture fixture) : CompanyTestBase(fixture)
{
    private const string Arc = "/api/v1/property/architectural-applications";

    // US3-S8
    [Fact]
    public async Task AfterTheArchive_JaneStillSeesHerHistory_AndHerSubmittedRequests_ButNotHerDraft()
    {
        var (companyId, community, home) = await CommunityWithHomeAsync();
        var (_, janeEmail) = await CreateResidentAsync(home);
        var (receiptId, transactionId) = await SeedPaymentHistoryAsync(home);
        var (applicationId, attachmentId) = await SeedDecidedApplicationAsync(community, home);
        var draftId = await WithDbAsync(async db =>
        {
            var draft = new ArchitecturalApplicationDraft
            {
                CommunityId = community, PropertyId = home, ProjectType = ArcProjectType.Fence, ProjectTitle = "Patio",
            };
            db.ArchitecturalApplicationDrafts.Add(draft);
            await db.SaveChangesAsync();
            return draft.Id;
        });
        await ArchiveAsync(companyId, community);

        using var jane = await ClientForAsync(janeEmail);
        var me = await JsonAsync(await jane.GetAsync("/api/v1/auth/me"));
        var listed = Assert.Single(me.GetProperty("properties").EnumerateArray());
        Assert.Equal((home, "Archived"), (listed.GetProperty("id").GetGuid(), listed.GetProperty("communityStatus").GetString()));

        foreach (var path in new[]
                 {
                     "/api/v1/payments/ledger?page=1&pageSize=20", "/api/v1/payments/statements",
                     "/api/v1/payments/transactions", $"/api/v1/payments/receipts/{receiptId}",
                     $"{Arc}/{applicationId}", $"{Arc}/{applicationId}/attachments/{attachmentId}/url",
                 })
        {
            var res = await jane.GetAsync(path);
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"{path}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
        var ledger = await JsonAsync(await jane.GetAsync("/api/v1/payments/ledger?page=1&pageSize=20"));
        Assert.Contains("Assessment", ledger.ToString());
        var transactions = await (await jane.GetAsync("/api/v1/payments/transactions")).Content.ReadAsStringAsync();
        Assert.Contains(transactionId.ToString(), transactions);

        // The list shows the submitted request and leaves the draft out (FR-012a).
        var list = await JsonAsync(await jane.GetAsync(Arc));
        var ids = list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Equal([applicationId], ids);

        var draft = await jane.GetAsync($"{Arc}/drafts/{draftId}");
        Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
        Assert.Equal("COMMUNITY_ARCHIVED", (await JsonAsync(draft)).GetProperty("code").GetString());
    }

    // US3-S9
    [Fact]
    public async Task ATokenFromBeforeTheArchive_CantEnrollOrPay()
    {
        var (companyId, community, home) = await CommunityWithHomeAsync();
        var (_, janeEmail) = await CreateResidentAsync(home);
        using var jane = await ClientForAsync(janeEmail);
        await ArchiveAsync(companyId, community);

        var enroll = await jane.PutAsJsonAsync("/api/v1/payments/recurring",
            new { amountType = "balance", draftDay = 1, setupIntentId = "seti_test", mandateAccepted = true });
        var pay = await jane.PostAsJsonAsync("/api/v1/payments/intent", new { amount = 10, method = "card" });

        foreach (var res in new[] { enroll, pay })
        {
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            Assert.Equal("COMMUNITY_ARCHIVED", (await JsonAsync(res)).GetProperty("code").GetString());
        }
        Assert.Equal(0, await WithDbAsync(db => db.RecurringPayments.CountAsync(r => r.PropertyId == home)));
        Assert.Equal(0, await WithDbAsync(db => db.PaymentTransactions.CountAsync(t => t.PropertyId == home)));
        Assert.Empty(Stripe.CreatedIntents);
    }

    // US3-S1 (claim code)
    [Fact]
    public async Task AClaimCode_ForAHomeInAnOnboardingCommunity_IsRefused_AndNotUsedUp()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId, CommunityStatus.Onboarding);
        var home = await CreatePropertyAsync(community);
        var email = $"lena-{Guid.NewGuid():N}@example.com";
        string code;
        using (var scope = Services.CreateScope())
            code = await scope.ServiceProvider.GetRequiredService<ClaimCodeService>().IssueAsync(home, email, default);
        var proof = await VerifiedEmailProofAsync(email);
        var client = CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            verificationToken = proof, password = Password, firstName = "Lena", lastName = "Ortiz", claimCode = code,
        });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("COMMUNITY_NOT_ACTIVE", (await JsonAsync(res)).GetProperty("code").GetString());
        var claim = await WithDbAsync(db => db.PropertyClaimCodes.AsNoTracking().SingleAsync(c => c.PropertyId == home));
        Assert.Equal((null, null), (claim.RedeemedAt, claim.RedeemedByUserId));
        Assert.False(await WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));
        Assert.False(await WithDbAsync(db => db.UserProperties.AnyAsync(up => up.PropertyId == home)));
        // The proof isn't used up either.
        Assert.True(await WithDbAsync(db => db.EmailVerifications.AnyAsync(v => v.ProofHash == AuthCrypto.Hash(proof) && v.ConsumedAt == null)));
    }

    private async Task<(Guid CompanyId, Guid Community, Guid Home)> CommunityWithHomeAsync()
    {
        var companyId = await CreateCompanyAsync();
        var community = await CreateCommunityInAsync(companyId);
        await CreateManagerAsync(community);
        return (companyId, community, await CreatePropertyAsync(community));
    }

    private async Task ArchiveAsync(Guid companyId, Guid community)
    {
        var (_, adminEmail) = await CreateCompanyAdminAsync(companyId);
        using var admin = await ClientForAsync(adminEmail);
        var res = await admin.PostAsync($"/api/v1/communities/{community}/archive", null);
        Assert.True(res.StatusCode == HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
    }

    private Task<(Guid ReceiptId, Guid TransactionId)> SeedPaymentHistoryAsync(Guid home) => WithDbAsync(async db =>
    {
        var ownerId = await db.Owners.Where(o => o.PropertyId == home).Select(o => o.Id).FirstAsync();
        var txn = new PaymentTransaction
        {
            Id = Guid.NewGuid(), PropertyId = home, OwnerId = ownerId, StripePaymentIntentId = $"pi_test_{Guid.NewGuid():N}",
            StripeChargeId = $"ch_test_{Guid.NewGuid():N}", GrossAmount = 250m, FeeAmount = 0m, Total = 250m,
            Status = TransactionStatus.Succeeded, PaymentMethod = PaymentMethod.Card,
        };
        db.PaymentTransactions.Add(txn);
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(), TransactionId = txn.Id, OwnerId = ownerId, ConfirmationNumber = "CONF-1001",
            MaskedMethod = "Visa •••• 4242", GrossAmount = 250m, FeeAmount = 0m, Total = 250m,
        };
        db.Receipts.Add(receipt);
        var today = DateOnly.FromDateTime(Clock.UtcNow.UtcDateTime);
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(), PropertyId = home, Sequence = 1, EntryDate = today.AddDays(-20), Description = "Assessment",
            ChargeAmount = 250m, RunningBalance = 250m, EntryType = LedgerEntryType.RegularAssessment,
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(), PropertyId = home, Sequence = 2, EntryDate = today.AddDays(-10), Description = "Payment",
            PaymentAmount = 250m, RunningBalance = 0m, EntryType = LedgerEntryType.Payment, TransactionId = txn.Id,
        });
        await db.SaveChangesAsync();
        return (receipt.Id, txn.Id);
    });

    // A request the board has decided and closed, so the archive has no open work to refuse on.
    private async Task<(Guid ApplicationId, Guid AttachmentId)> SeedDecidedApplicationAsync(Guid community, Guid home)
    {
        var applicationId = await CreateArcApplicationAsync(community, home, 1001, ArcApplicationStatus.Closed);
        var key = $"arc/{community}/1001/{Guid.NewGuid()}";
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IDocumentStorage>().UploadAsync(key, "%PDF-1.4"u8.ToArray(), "application/pdf");
        var attachmentId = await WithDbAsync(async db =>
        {
            var attachment = new ArchitecturalAttachment
            {
                ApplicationId = applicationId, FileName = "plans.pdf", SizeBytes = 8, ContentType = "application/pdf", StorageKey = key,
            };
            db.ArchitecturalAttachments.Add(attachment);
            await db.SaveChangesAsync();
            return attachment.Id;
        });
        return (applicationId, attachmentId);
    }

    private Task<string> VerifiedEmailProofAsync(string email) => WithDbAsync(async db =>
    {
        var proof = AuthCrypto.NewProofToken();
        db.EmailVerifications.Add(new EmailVerification
        {
            Email = email.ToLowerInvariant(), Purpose = EmailVerificationService.PurposeRegistration,
            CodeHash = AuthCrypto.Hash("000000"), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            ConfirmedAt = DateTimeOffset.UtcNow, ProofHash = AuthCrypto.Hash(proof),
            ProofExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
        });
        await db.SaveChangesAsync();
        return proof;
    });
}
