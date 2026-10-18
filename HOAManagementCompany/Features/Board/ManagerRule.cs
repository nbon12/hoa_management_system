using HOAManagementCompany.Domain.Enums;

namespace HOAManagementCompany.Features.Board;

// <!-- REPOWISE:START domain=board -->
// The open-ended-manager rule (030 FR-020; research R7), pure and unit-tested. A community "has a
// manager" only while at least one Community Manager membership is active with no end date. A
// change that would leave a community that has one with none — now or on a later date — is
// refused with LAST_MANAGER. A first manager must be open-ended, so an end-dated appointment needs
// another open-ended manager to remain. Archived communities skip the rule (FR-012). Callers run
// it inside CommunityLocks so concurrent changes see each other (US2-S14).
// <!-- REPOWISE:END -->

public static class ManagerRule
{
    /// <summary>One CommunityManager membership row of the community.</summary>
    public sealed record Row(Guid Id, MembershipStatus Status, DateOnly? EndDate)
    {
        public bool IsOpenEnded => Status == MembershipStatus.Active && EndDate is null;
    }

    /// <summary>A proposed change to the community's manager rows.</summary>
    public sealed record Change(ChangeKind Kind, Guid? RowId, DateOnly? EndDate)
    {
        public static Change End(Guid rowId) => new(ChangeKind.End, rowId, null);
        public static Change Downgrade(Guid rowId) => new(ChangeKind.End, rowId, null);
        public static Change Deactivate(Guid rowId) => new(ChangeKind.End, rowId, null);
        public static Change SetEndDate(Guid rowId, DateOnly? endDate) => new(ChangeKind.SetEndDate, rowId, endDate);
        /// <summary>A direct appointment, a re-appointment, or a manager invitation being accepted.</summary>
        public static Change Appoint(DateOnly? endDate) => new(ChangeKind.Appoint, null, endDate);
        /// <summary>The one-action replace: an open-ended replacement, then the current row ends.</summary>
        public static Change Replace(Guid currentRowId) => new(ChangeKind.Replace, currentRowId, null);
    }

    public enum ChangeKind
    {
        End,
        SetEndDate,
        Appoint,
        Replace
    }

    public static bool HasOpenEndedManager(IEnumerable<Row> rows) => rows.Any(r => r.IsOpenEnded);

    /// <summary>True when the change is allowed; false means LAST_MANAGER.</summary>
    public static bool Evaluate(IReadOnlyCollection<Row> rows, Change change, DateOnly today, bool archived)
    {
        if (archived)
            return true;

        var hadManager = HasOpenEndedManager(rows);
        var after = Apply(rows, change, today);

        if (change.Kind == ChangeKind.Appoint && !hadManager && change.EndDate is not null)
            return false; // A first manager must be open-ended.

        return !hadManager || HasOpenEndedManager(after);
    }

    private static List<Row> Apply(IReadOnlyCollection<Row> rows, Change change, DateOnly today)
    {
        var after = rows.ToList();
        switch (change.Kind)
        {
            case ChangeKind.End:
                Replace(after, change.RowId, r => r with { Status = MembershipStatus.Inactive, EndDate = today });
                break;
            case ChangeKind.SetEndDate:
                Replace(after, change.RowId, r => r with { EndDate = change.EndDate });
                break;
            case ChangeKind.Appoint:
                after.Add(new Row(Guid.NewGuid(), MembershipStatus.Active, change.EndDate));
                break;
            case ChangeKind.Replace:
                after.Add(new Row(Guid.NewGuid(), MembershipStatus.Active, null));
                Replace(after, change.RowId, r => r with { Status = MembershipStatus.Inactive, EndDate = today });
                break;
        }
        return after;
    }

    private static void Replace(List<Row> rows, Guid? rowId, Func<Row, Row> update)
    {
        var index = rows.FindIndex(r => r.Id == rowId);
        if (index >= 0)
            rows[index] = update(rows[index]);
    }
}
