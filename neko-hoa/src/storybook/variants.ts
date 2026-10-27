import type { StoryObj } from '@storybook/angular';

// 030 T149: story parameters shared by the company-administration stories. Each component's stories
// file exports Desktop, DesktopDark, Tablet, Phone and PhoneDark built from one base story, so every
// component is seen at the three conventions §2 widths (1360, 834, 390) and in both themes
// (.storybook/preview.ts).

// Story globals, not parameters: Storybook 10 picks the viewport from `globals.viewport` and ignores
// the old `parameters.viewport.defaultViewport`.
export const DESKTOP = { viewport: { value: 'desktop', isRotated: false } };
export const TABLET = { viewport: { value: 'tablet', isRotated: false } };
export const PHONE = { viewport: { value: 'phone', isRotated: false } };
export const DARK = { theme: 'dark' };

type Size = 'desktop' | 'tablet' | 'phone';

/**
 * `const at = storyAt<MyComponent>();` then `at('phone', base, true)`: `base` at one width, in the
 * dark theme when `dark`.
 */
export function storyAt<T>() {
  return (size: Size, base: StoryObj<T> = {}, dark = false): StoryObj<T> => {
    const viewport = size === 'desktop' ? DESKTOP : size === 'tablet' ? TABLET : PHONE;
    return {
      ...base,
      parameters: { ...(base.parameters ?? {}), layout: 'fullscreen' },
      globals: { ...(base.globals ?? {}), ...viewport, ...(dark ? DARK : {}) },
    };
  };
}
