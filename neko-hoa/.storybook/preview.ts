import type { Preview } from '@storybook/angular';
import { MINIMAL_VIEWPORTS } from '@storybook/addon-viewport';

// 030 T149: every story can be seen at the phone, tablet and desktop widths of conventions §2
// (390, 834 and 1360 px) and in both themes. The app ships one theme; "Dark" redefines the design
// tokens (preview-head.html), so a story that still reads well proves its component uses tokens
// only, with no hard-coded colours.
const preview: Preview = {
  parameters: {
    controls: {
      matchers: { color: /(background|color)$/i, date: /Date$/i },
    },
    viewport: {
      viewports: {
        ...MINIMAL_VIEWPORTS,
        phone: { name: 'Phone (390)', styles: { width: '390px', height: '844px' }, type: 'mobile' },
        tablet: { name: 'Tablet (834)', styles: { width: '834px', height: '1112px' }, type: 'tablet' },
        desktop: { name: 'Desktop (1360)', styles: { width: '1360px', height: '900px' }, type: 'desktop' },
      },
    },
  },
  globalTypes: {
    theme: {
      description: 'Design-token theme',
      toolbar: {
        title: 'Theme',
        icon: 'mirror',
        items: [{ value: 'light', title: 'Light' }, { value: 'dark', title: 'Dark' }],
        dynamicTitle: true,
      },
    },
  },
  initialGlobals: { theme: 'light' },
  decorators: [
    (storyFn, context) => {
      document.documentElement.setAttribute('data-theme', context.globals['theme'] === 'dark' ? 'dark' : 'light');
      return storyFn();
    },
  ],
};

export default preview;
