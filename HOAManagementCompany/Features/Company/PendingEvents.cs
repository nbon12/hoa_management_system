namespace HOAManagementCompany.Features.Company;

// 030 FR-040, R5: a change event written inside a locked transaction is held until that transaction
// commits. The lock runs its body in a retrying execution strategy, so a retried attempt writes its
// events once, and an attempt that rolls back writes none. AdvisoryLock.RunAsync starts a batch for
// each attempt and writes it after the commit; outside a lock, events are written at once.
internal static class PendingEvents
{
    private static readonly AsyncLocal<List<Action>?> Batch = new();

    public static void Write(Action write)
    {
        if (Batch.Value is { } batch)
            batch.Add(write);
        else
            write();
    }

    /// <summary>Starts a batch for the calling async flow. The caller writes it once its transaction commits.</summary>
    public static List<Action> Begin() => Batch.Value = [];
}
