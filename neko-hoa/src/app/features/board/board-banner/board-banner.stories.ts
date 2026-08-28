import type { Meta, StoryObj } from '@storybook/angular';
import { BoardBannerComponent } from './board-banner.component';

// 025 T027 (FR-021) — visual-regression story for the board-mode banner. The banner marks
// board mode and states that association-wide data is shown; it carries no control. Binding
// contrast is dark ink on the violet fill (white-on-violet fails WCAG 2.1 AA, FR-038).
const meta: Meta<BoardBannerComponent> = {
  title: 'Board/BoardBanner',
  component: BoardBannerComponent,
};

export default meta;
type Story = StoryObj<BoardBannerComponent>;

/** The board-mode banner as rendered at the top of the shell while in board mode. */
export const Default: Story = {};
