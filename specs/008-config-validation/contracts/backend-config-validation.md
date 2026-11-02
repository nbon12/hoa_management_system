# Contract: Backend Startup Configuration Validation

This feature exposes no HTTP endpoints. Its "contract" is the **startup behavior** of the
backend host and the registration surface used to wire validated options.

## Behavioral contract

| # | Given | When | Then |
|---|-------|------|------|
| C1 | Any option group violates a rule (any environment) | host starts | host **fails to start**; throws `OptionsValidationException`; failure written to startup logs/console naming `Section:Field` and reason (FR-001, FR-002). |
| C2 | Multiple fields within a section are invalid | host starts | the thrown error lists **all** failures for that section (FR-003). |
| C3 | A required secret is unset/whitespace (any environment) | host starts | host fails to start naming the missing secret (FR-004). |
| C4 | Valid config with placeholder secrets (Dev/Test) | host starts | host starts successfully (FR-005, FR-016, SC-003). |
| C5 | `Storage` section missing/empty | host starts | host fails to start with a clear storage error — **never** a `NullReferenceException` (FR-011). |
| C6 | Optional provider (Twilio/SES; SES replaced SendGrid per specs/026-ses-email-provider) fully unset | host starts | host starts; provider disabled (FR-012). |
| C7 | Optional provider partially configured | host starts | host fails to start naming the missing provider field(s) (FR-012). |
| C8 | Error message construction | any failure | message MUST NOT contain any secret value (FR-019). |

## Registration surface (DI extension)

```csharp
// Replaces builder.Services.Configure<T>(section) for every option group.
builder.Services
    .AddValidatedOptions<StripeOptions, StripeOptionsValidator>(builder.Configuration, StripeOptions.SectionName);
// ...repeat for Payments, Jobs, Twilio, Ses (replaced SendGrid per specs/026-ses-email-provider), Storage, Observability.

// AddValidatedOptions internally:
//   services.AddSingleton<IValidator<TOptions>, TValidator>();
//   services.AddSingleton<IValidateOptions<TOptions>>(sp => new FluentValidateOptions<TOptions>(
//       Options.DefaultName, sp.GetRequiredService<IValidator<TOptions>>()));
//   services.AddOptions<TOptions>().Bind(configuration.GetSection(sectionName)).ValidateOnStart();
// An overload taking Action<TOptions> postConfigure applies it after Bind, before validation.
```

## Generic adapter contract

```csharp
public sealed class FluentValidateOptions<T>(string? registeredName, IValidator<T> validator)
    : IValidateOptions<T> where T : class
{
    public ValidateOptionsResult Validate(string? name, T options)
    {
        if (registeredName is not null && registeredName != name) return ValidateOptionsResult.Skip;
        var result = validator.Validate(options);
        if (result.IsValid) return ValidateOptionsResult.Success;
        var failures = result.Errors.Select(e => $"{typeof(T).Name}.{e.PropertyName}: {e.ErrorMessage}");
        return ValidateOptionsResult.Fail(failures); // never includes raw values (FR-019)
    }
}
```

## Test contract (xUnit)

- **Unit (per validator, Theories)**: pass/fail rows per rule — boundary ratios (0, 1, -0.01,
  1.01), enum values, `CardFeeType=Percentage`+`CardScope=AllCards` (reject), non-negative fee
  amounts, webhook tolerance, partial provider permutations (FR-014, FR-015).
- **Integration (startup)**: `WebApplicationFactory<Program>` with bad config →
  `Assert.Throws<OptionsValidationException>`; valid Test config with placeholders → starts
  (FR-014, FR-016). No PostgreSQL needed for failure cases.
