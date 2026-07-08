using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HOAManagementCompany.Tests.Fixtures;

/// <summary>
/// Thrown by <see cref="FaultInjectionInterceptor"/> to simulate a crash/transient outage at an
/// exact persistence point. Deliberately NOT a transient Npgsql error, so the EF execution
/// strategy propagates it (rolling back any open transaction) instead of retrying in place —
/// exactly what a process crash between writes looks like to the database.
/// </summary>
public sealed class SimulatedCrashException() : Exception("Simulated crash (fault injection)");

/// <summary>
/// Interrupt-and-retry harness for the payment-integrity tests (015 US1, FR-001/FR-002):
/// throws once from <c>SavingChanges</c> when the armed predicate matches the change-tracker
/// state, then disarms. Register one shared instance in the DbContext options so the armed
/// state spans the per-delivery scopes a real webhook retry would use.
/// </summary>
public sealed class FaultInjectionInterceptor : SaveChangesInterceptor
{
    private Func<DbContext, bool>? _shouldFault;

    /// <summary>Arms a one-shot fault fired on the first SaveChanges where the predicate holds.</summary>
    public void ArmOnce(Func<DbContext, bool> predicate) => _shouldFault = predicate;

    public void Disarm() => _shouldFault = null;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        MaybeFault(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        MaybeFault(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void MaybeFault(DbContextEventData eventData)
    {
        var ctx = eventData.Context;
        if (ctx is null || _shouldFault is null || !_shouldFault(ctx)) return;
        _shouldFault = null;   // one-shot: the retry after the "crash" must run clean
        throw new SimulatedCrashException();
    }
}
