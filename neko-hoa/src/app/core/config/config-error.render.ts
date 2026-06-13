/**
 * Renders a minimal, static, full-page error when required configuration is missing, instead of
 * bootstrapping the Angular app (008-config-validation, FR-017). Deliberately framework-free so it
 * works before/without Angular and produces a perceivable DOM message (not a console-only failure).
 */
export function renderConfigError(missingKeys: string[], doc: Document = document): void {
  const items = missingKeys.map((key) => `<li><code>${escapeHtml(key)}</code></li>`).join('');
  const isPlural = missingKeys.length !== 1;

  doc.body.innerHTML = `
    <main role="alert" aria-live="assertive"
          style="font-family: system-ui, -apple-system, sans-serif; max-width: 40rem;
                 margin: 4rem auto; padding: 1.5rem; border: 2px solid #b00020;
                 border-radius: 8px; color: #1a1a1a; line-height: 1.5;">
      <h1 style="color: #b00020; font-size: 1.25rem; margin-top: 0;">Application configuration error</h1>
      <p>The application cannot start because the following required configuration
         ${isPlural ? 'values are' : 'value is'} missing:</p>
      <ul>${items}</ul>
      <p>This is a deployment configuration problem. Please contact your administrator.</p>
    </main>`;
}

function escapeHtml(value: string): string {
  const map: Record<string, string> = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;',
  };
  return value.replace(/[&<>"']/g, (char) => map[char]);
}
