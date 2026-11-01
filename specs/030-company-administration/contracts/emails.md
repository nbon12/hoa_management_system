# Contract: Emails

Plain text for now. The HTML layouts come from Claude Design brief 12 ([design/12-emails.md](../design/12-emails.md)) and replace the body builders without changing these fields.

- **Time zones**: expiry and other times are shown in the community's time zone (`CommunityArcSettings.TimeZoneId`). Company Administrator invitations use the company default's zone.
- **Links** use `App:BaseUrl`. Today's `ArchitecturalReview:AppBaseUrl` stays as a fallback (R10).
- **Logging**: no email body is ever logged.

## Invitation emails: sent directly, not through the outbox (R6)

These are sent through the email `IAlertProvider` at send time. Subjects and bodies use design brief 12's §5.2 copy, in its §5.4 plain-text form. Each attempt is recorded in `InvitationDeliveries`. On failure the invitation keeps `LastSendError`, and the lists offer Resend.

| Template | When | Subject | Body fields |
|---|---|---|---|
| Role invitation | membership invitation sent or resent | "You're invited to be a {role} of {community}" ("an Accountant") | community, role, term end (if any), sender name, **link**, expiry ("The link works once, and only for {email}. It expires {local time}, 72 hours after it was sent."), how to ignore it |
| Resident invitation | residency invitation sent or resent, including the activation fan-out | "You're invited to be a resident of {property address}" | community, property address, sender name, **link**, expiry, and how to accept: sign in ("Sign in to accept") when the email already has an account, otherwise create an account with no claim code ("Accept invitation") |
| Company Administrator invitation | `POST /company/administrators/invitations`, or the setup step | "You're invited to be a Company Administrator of {company}" | company, sender ("Sent by: Setup step" for the setup step), **link**, expiry |

## Notification emails: through the outbox (R10)

All recipients have accounts, so these use `RecipientUserId`. Dispatched inline after commit, with the reconcile backstop.

| Kind (≤ 30 characters) | Recipient | Subject | Body fields | Dedup key |
|---|---|---|---|---|
| `co_community_added` | every Company Administrator | "{community} was added and is waiting to be marked Active" | community name, the Community Manager's name, link to its Community settings | `co:community-added:{communityId}:{userId}` |
| `co_community_archived` | every resident linked to a property in the community | "{company} no longer manages {community}" | community, the property or properties this resident is linked to, "Your payment history stays readable.", and "Autopay for {property} has ended." for each property whose schedule was cancelled | `co:archived:{communityId}:{archivedAt:yyyyMMddHHmmss}:{userId}` |
| `co_resident_removed` | the removed resident | "You were removed from {property address}" | property, community, "Autopay you set up for this home has ended." when it applied, and "Contact your community manager if this is a mistake." | `co:removed:{propertyId}:{userId}:{removedAt:yyyyMMddHHmmss}` |
| `co_autopay_ended` | each other resident of the property, when a removal cancelled its autopay | "Autopay for {property address} has ended" | property, "set it up again from Payments if you want it" (no name of the removed resident) | `co:autopay-ended:{recurringId}:{userId}:{at:yyyyMMddHHmmss}` |
| `co_admin_recovery` | every current Company Administrator | "A Company Administrator was added in recovery mode" | the granted or invited user's name or email, UTC time, "This was done from the command line by your deployment's operator." | `co:recovery:{grantOrInvitationId}:{userId}` |

**Not sent**:
- to the owner of record on any of these actions (spec Out of Scope);
- during the migration of `Inactive` communities (spec Database/runtime).
