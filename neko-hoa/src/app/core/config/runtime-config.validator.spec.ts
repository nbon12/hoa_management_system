import { findMissingRequiredConfig, RuntimeConfig } from './runtime-config.validator';
import { renderConfigError } from './config-error.render';

describe('findMissingRequiredConfig', () => {
  const base: RuntimeConfig = {
    production: true,
    apiBaseUrl: 'https://api.example.com',
    stripePublishableKey: 'pk_live_x',
  };

  it('returns [] when a production config is complete', () => {
    expect(findMissingRequiredConfig(base)).toEqual([]);
  });

  it('flags an empty stripePublishableKey in production', () => {
    expect(findMissingRequiredConfig({ ...base, stripePublishableKey: '' })).toEqual([
      'stripePublishableKey',
    ]);
  });

  it('flags a blank (whitespace) apiBaseUrl in production', () => {
    expect(findMissingRequiredConfig({ ...base, apiBaseUrl: '   ' })).toEqual(['apiBaseUrl']);
  });

  it('flags multiple missing values', () => {
    const missing = findMissingRequiredConfig({ ...base, apiBaseUrl: '', stripePublishableKey: '' });
    expect(missing).toContain('apiBaseUrl');
    expect(missing).toContain('stripePublishableKey');
    expect(missing.length).toBe(2);
  });

  it('never blocks non-production builds, even when values are empty', () => {
    expect(
      findMissingRequiredConfig({ production: false, apiBaseUrl: '', stripePublishableKey: '' }),
    ).toEqual([]);
  });
});

describe('renderConfigError', () => {
  it('renders a perceivable full-page DOM message naming the missing keys', () => {
    const doc = document.implementation.createHTMLDocument('test');
    renderConfigError(['stripePublishableKey'], doc);

    expect(doc.body.querySelector('[role="alert"]')).not.toBeNull();
    expect(doc.body.textContent).toContain('configuration');
    expect(doc.body.textContent).toContain('stripePublishableKey');
  });

  it('lists every missing key', () => {
    const doc = document.implementation.createHTMLDocument('test');
    renderConfigError(['apiBaseUrl', 'stripePublishableKey'], doc);

    expect(doc.body.querySelectorAll('li').length).toBe(2);
    expect(doc.body.textContent).toContain('apiBaseUrl');
  });
});
