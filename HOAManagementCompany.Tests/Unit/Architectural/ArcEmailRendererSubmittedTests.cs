using HOAManagementCompany.Domain.Entities;
using HOAManagementCompany.Domain.Enums;
using HOAManagementCompany.Features.Board.Architectural;
using Microsoft.Extensions.Options;
using Xunit;

namespace HOAManagementCompany.Tests.Unit.Architectural;

/// <summary>029 T026 / FR-025 — the plain-template submission confirmation.</summary>
public class ArcEmailRendererSubmittedTests
{
    private static readonly ArcEmailRenderer Renderer =
        new(Options.Create(new ArchitecturalReviewOptions { AppBaseUrl = "https://app.example.com" }));

    private static ArchitecturalApplication App(int revision = 1) => new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        ApplicationNumber = 1042, Revision = revision, ProjectType = ArcProjectType.Fence,
        ProjectTitle = "Fence replacement — 6ft cedar",
        ReceivedDate = new DateOnly(2026, 5, 28), DueDate = new DateOnly(2026, 6, 27)
    };

    [Fact]
    public void OwnerSubmitted_RendersDisplayIdDatesAndNoBoardData()
    {
        var message = Renderer.OwnerSubmitted(App(), "711 Keystone Park Dr #29", "Praneeth", "owner@example.com", "Neko Village");

        Assert.Equal("owner@example.com", message.Target);
        Assert.Equal("We received your architectural request ARC-1042", message.Subject);
        Assert.Contains("Request: ARC-1042", message.Body);
        Assert.Contains("Project: Fence replacement — 6ft cedar", message.Body);
        Assert.Contains("Submitted: 05/28/26", message.Body);
        Assert.Contains("Decision due by: 06/27/26", message.Body);
        Assert.Contains("Work may not begin until this request is approved.", message.Body);
        Assert.Contains("https://app.example.com/app/property/architectural/11111111-2222-3333-4444-555555555555", message.Body);
        Assert.DoesNotContain("vote", message.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OwnerSubmitted_ForARevision_ShowsTheRevisionBadge()
    {
        var message = Renderer.OwnerSubmitted(App(revision: 2), "711 Keystone Park Dr #29", "Praneeth", "owner@example.com", "Neko Village");

        Assert.Contains("Request: ARC-1042 v2", message.Body);
    }

    [Fact]
    public void OwnerKind_RefusesWithdrawn_SoAWithdrawalCanNeverSendAnOutcomeEmail()
    {
        Assert.Throws<InvalidOperationException>(() => ArcEmailRenderer.OwnerKind(ArcOutcome.Withdrawn, null));
    }
}
