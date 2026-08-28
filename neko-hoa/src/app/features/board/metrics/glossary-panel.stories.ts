import type { Meta, StoryObj } from '@storybook/angular';
import { GlossaryPanelComponent } from './glossary-panel.component';
import { MetricDescriptor } from '../../../core/services/board.service';

// 025 T056 (US4) — visual-regression stories for the glossary panel. Definitions derive from
// the SAME descriptors the table renders (FR-034), so a definition can never drift from its
// metric. Opening from a row scrolls to and visually distinguishes that entry ("jumped here").
const DESCRIPTORS: MetricDescriptor[] = [
  { id: 'over30', label: 'Over 30-Days Delinquent', definitionText: 'The share of homeowners whose balance has been unpaid for more than 30 days past the assessment due date.', value: '10%', status: 'warn', emphasis: 'warn' },
  { id: 'over60', label: 'Over 60-Days Delinquent', definitionText: 'The share of homeowners whose balance has been unpaid for more than 60 days.', value: '4%', status: 'warn', emphasis: 'warn' },
  { id: 'ach',    label: 'Registered ACH Owners',   definitionText: 'The share of owners enrolled in automatic bank draft (ACH) for recurring assessments.', value: '60%', status: 'ok', emphasis: 'ok' },
  { id: 'reserve',label: 'Reserve Funding',         definitionText: 'The reserve fund balance expressed as a percentage of the board-adopted funding target.', value: '82%', status: 'ok', emphasis: 'none' },
];

const meta: Meta<GlossaryPanelComponent> = {
  title: 'Board/GlossaryPanel',
  component: GlossaryPanelComponent,
};

export default meta;
type Story = StoryObj<GlossaryPanelComponent>;

/** The glossary listing every definition for the page, none highlighted. */
export const Default: Story = {
  args: { descriptors: DESCRIPTORS, target: null },
};

/** Opened from a metric's help affordance: the matching entry is highlighted ("jumped here"). */
export const JumpedToTerm: Story = {
  args: { descriptors: DESCRIPTORS, target: 'ach' },
};
