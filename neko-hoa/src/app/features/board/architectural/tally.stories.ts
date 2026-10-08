import type { Meta, StoryObj } from '@storybook/angular';
import { TallyComponent } from './tally.component';

// 027 T029/T075 — the board vote tally (FR-007): approve · revisions needed · deny · not voted.
const meta: Meta<TallyComponent> = { title: 'Board/Architectural/Tally', component: TallyComponent };
export default meta;
type Story = StoryObj<TallyComponent>;

export const TwoOfFive: Story = { args: { tally: { approve: 2, revisionsNeeded: 0, deny: 0, notVoted: 3, eligible: 5 } } };
export const Mixed: Story = { args: { tally: { approve: 1, revisionsNeeded: 2, deny: 1, notVoted: 1, eligible: 5 } } };
export const AllVoted: Story = { args: { tally: { approve: 3, revisionsNeeded: 1, deny: 1, notVoted: 0, eligible: 5 } } };
