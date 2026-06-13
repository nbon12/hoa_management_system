# Quickstart: Startup Configuration Validation

How to implement, verify, and extend this feature.

## Prerequisites

- .NET 9.0 SDK; Node + Angular CLI for the frontend.
- FluentValidation reference added to `HOAManagementCompany.csproj` (version-aligned with
  FastEndpoints — see research R2).

## Backend — add a validated option group

1. Write the validator in `HOAManagementCompany/Infrastructure/Configuration/`:

   ```csharp
   using FluentValidation;
   public sealed class PaymentsOptionsValidator : AbstractValidator<PaymentsOptions>
   {
       public PaymentsOptionsValidator()
       {
           RuleFor(x => x.VariableNoticeLeadDays).GreaterThanOrEqualTo(0);
           RuleFor(x => x.ReconcilePendingAchAfterHours).GreaterThan(0);
           RuleFor(x => x.DefaultFee.CardFeeType)
               .Must(t => t is "Flat" or "Percentage").WithMessage("CardFeeType must be Flat or Percentage.");
           RuleFor(x => x.DefaultFee.CardScope)
               .Equal("CreditOnly")
               .When(x => string.Equals(x.DefaultFee.CardFeeType, "Percentage", StringComparison.OrdinalIgnoreCase))
               .WithMessage("Percentage fees must be scoped to CreditOnly.");
           RuleFor(x => x.DefaultFee.CardFeeValue).GreaterThanOrEqualTo(0);
           RuleFor(x => x.DefaultFee.AchFeeValue).GreaterThanOrEqualTo(0);
       }
   }
   ```

2. Register it in `Program.cs`, replacing the plain `Configure<T>`:

   ```csharp
   builder.Services.AddValidatedOptions<PaymentsOptions, PaymentsOptionsValidator>(
       PaymentsOptions.SectionName);
   ```

3. Repeat for `Stripe`, `Jobs`, `Twilio`, `SendGrid`, `Storage`, `Observability`. Remove the
   null-forgiving `GetSection("Storage").Get<StorageOptions>()!` and resolve
   `IOptions<StorageOptions>` for the `IAmazonS3` singleton instead.

## Backend — verify

```bash
# Fails fast with a clear error (bad value):
Payments__DefaultFee__CardFeeType=Percentage Payments__DefaultFee__CardScope=AllCards \
  dotnet run --project HOAManagementCompany
# → OptionsValidationException: "PaymentsOptions: Percentage fees must be scoped to CreditOnly."

# Starts cleanly with valid config + placeholder secrets.
dotnet run --project HOAManagementCompany
```

Run the tests:

```bash
dotnet test HOAManagementCompany.Tests \
  --filter "FullyQualifiedName~Configuration"
```

## Frontend — verify

```bash
cd neko-hoa
# Production build with an empty publishable key → full-page config error on load.
npm test            # Jasmine/Karma unit tests for the guard
npm run lint
```

## Definition of done (maps to spec)

- [ ] All 7 backend option groups registered via `AddValidatedOptions` + `ValidateOnStart`
      (SC-001).
- [ ] Bad config → host fails to start with a `Section:Field` message; no secret values in
      messages (C1/C8, FR-019).
- [ ] `Storage` missing → clear error, no `NullReferenceException` (C5/FR-011).
- [ ] Optional providers: unset = OK, partial = rejected (C6/C7, FR-012).
- [ ] Per-validator unit tests (Theories) + startup fail-fast tests + valid-Test-config-starts
      test all pass; ≥95% coverage on changed files (FR-014–FR-016, SC-004).
- [ ] Frontend guard halts bootstrap with full-page error in production when required values
      missing; never blocks dev (F1–F5, SC-006).
- [ ] Repowise marker regions added/refreshed on new files (constitution CI/CD).
