import type { StoryObj } from '@storybook/angular';

// 030 T149: story parameters shared by the company-administration stories. Each component's stories
// file exports Desktop, DesktopDark, Tablet, Phone and PhoneDark built from one base story, so every
// component is seen at the three conventions §2 widths (1360, 834, 390) and in both themes
// (.storybook/preview.ts).

export const DESKTOP = { viewport: { defaultViewport: 'desktop' }, layout: 'fullscreen' };
export const TABLET = { viewport: { defaultViewport: 'tablet' }, layout: 'fullscreen' };
export const PHONE = { viewport: { defaultViewport: 'phone' }, layout: 'fullscreen' };
export const DARK = { theme: 'dark' };

type Size = 'desktop' | 'tablet' | 'phone';

/**
 * `const at = storyAt<MyComponent>();` then `at('phone', base, true)`: `base` at one width, in the
 * dark theme when `dark`.
 */
export function storyAt<T>() {
  return (size: Size, base: StoryObj<T> = {}, dark = false): StoryObj<T> => {
    const viewport = size === 'desktop' ? DESKTOP : size === 'tablet' ? TABLET : PHONE;
    return { ...base, parameters: { ...(base.parameters ?? {}), ...viewport }, ...(dark ? { globals: { ...DARK } } : {}) };
  };
}
