# Stripe Payment Integration

_Started 2026-06-06 02:15 UTC_

---

## User

I'd like to redesign the one-time and recurring billing pages to use Stripe instead of collecting the card and bank info directly in the page. come up with some ideas for what this could look like.

## Assistant

_[tool: list_files]_

_[tool: read_file]_

_[tool: read_file]_

_[tool: read_file]_

_[tool: read_file]_

_[tool: update_todos]_

This is a clean, well-built wireframe set in a low-fi pastel style on a design canvas. I understand the current billing pages collect raw card numbers (`4242 4242…`) and bank routing/account numbers directly. The redesign goal: hand that sensitive collection off to Stripe so NekoHOA never touches PAN/bank data.

Let me lay out the plan and build a spread of Stripe-pattern ideas as new artboards, matching the existing wireframe vocabulary exactly.

_[tool: write_file]_

Now let me build the Stripe wireframes file:

_[tool: update_todos]_

_[tool: str_replace_edit]_

_[tool: show_html]_

_[tool: show_html]_

_[tool: get_webview_logs]_

_[tool: grep]_

No errors. Let me focus one of the Stripe artboards to verify it renders correctly:

_[tool: save_screenshot]_

Let me focus a Stripe artboard to verify it renders properly:

_[tool: save_screenshot]_

The embedded Payment Element variant renders cleanly — the Stripe region with "stripe iframe" tag, method tabs, and the secured-fields treatment all read correctly. Let me check the hosted-checkout and recurring-portal variants too:

_[tool: save_screenshot]_

_[tool: update_todos]_

All seven variants render correctly and match the existing wireframe vocabulary. Let me finalize.

_[tool: done]_

_[tool: fork_verifier_agent]_

I added a new **"3b · Billing on Stripe (redesign)"** section to your wireframe canvas with 7 artboards exploring how to hand card/bank collection to Stripe. The through-line: NekoHOA stops typing raw PANs and routing/account numbers — Stripe collects and vaults them, and you keep only a token + "•• last4".

**One-time payment — 3 ideas**
- **A · Hosted Checkout redirect** — pick the amount on our page, then "Continue to secure checkout" hands off to Stripe's hosted page (shown as the 2nd artboard in browser chrome). Least code, lowest PCI scope.
- **B · Embedded Payment Element** — Stripe's payment box renders *inside* our page (Card / US bank / Wallet tabs), no redirect. I gave it a distinct "stripe iframe" boundary treatment so it reads as Stripe-owned.
- **C · Vaulted methods, one-tap** — show saved methods (Fidelity ••747, Visa ••4242), tap and pay. Fastest for repeat payers.

**Recurring / auto-pay — 3 ideas**
- **A · Rules ours, method via Stripe** — keep the "what gets paid / draft date" config; the bank/card is a vaulted token with Add/Replace opening a Stripe sheet.
- **B · Embedded setup (SetupIntent)** — one screen: pick rules, then authorize the method + ACH mandate in an embedded Stripe region.
- **C · Customer Portal handoff** — lightest build; we show status, Stripe's billing portal handles swaps, pause/cancel, receipts, retries, dunning.

A few things worth your call before going further:
- **Fees** — I kept your card-fee / free-ACH model but moved the exact total into Stripe's confirmation. Worth confirming how you want surcharging presented (some states restrict it).
- **Which patterns to carry forward** — these span a real effort spectrum (Portal/Checkout = least build → Embedded Element = most control). Happy to drop the ones you don't want and flesh out the winners into the higher-fidelity style.

Want me to narrow to one direction per page and detail the success/decline/retry states?

