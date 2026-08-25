import type { Meta, StoryObj } from '@storybook/angular';
import { MetricTableComponent } from './metric-table.component';
import { MetricDescriptor } from '../../../core/services/board.service';

// 025 T056 (US4) — visual-regression stories for the registry-driven metric table. One table
// renders every metric surface; the help affordance is the right-most column of every row
// (FR-032). Stories cover the populated table, a headline "hero statistics" surface, the
// explicit empty state (FR-030), and a per-row unavailable state that does not blank siblings
// (FR-036).

// A community-metrics surface: values with status + emphasis + detail lines.
const COMMUNITY_METRICS: MetricDescriptor[] = [
  { id: 'over30', label: 'Over 30-Days Delinquent', definitionText: 'Share of homeowners more than 30 days past due.', value: '10%', detail: '37 homeowners', status: 'warn', emphasis: 'warn' },
  { id: 'over60', label: 'Over 60-Days Delinquent', definitionText: 'Share of homeowners more than 60 days past due.', value: '4%',  detail: '15 homeowners', status: 'warn', emphasis: 'warn' },
  { id: 'ach',    label: 'Registered ACH Owners',   definitionText: 'Owners enrolled in automatic bank draft.',       value: '60%', detail: null,           status: 'ok',   emphasis: 'ok' },
  { id: 'reserve',label: 'Reserve Funding',         definitionText: 'Reserve balance vs. the funding target.',         value: '82%', detail: 'target 90%',   status: 'ok',   emphasis: 'none' },
];

// A "work processed" hero surface: headline counts, no status column (showStatus off).
const HERO_STATS: MetricDescriptor[] = [
  { id: 'arc',      label: 'Architectural Applications', definitionText: 'ARC requests processed in the last 30 days.', value: 24,  detail: '6 pending',   status: 'ok', emphasis: 'link' },
  { id: 'approvals',label: 'Board Approvals',            definitionText: 'Board approvals recorded in the last 30 days.', value: 11, detail: null,          status: 'ok', emphasis: 'none' },
  { id: 'violations',label: 'Violations Opened',         definitionText: 'New violations opened in the last 30 days.',   value: 8,  detail: '3 resolved',  status: 'ok', emphasis: 'none' },
];

const meta: Meta<MetricTableComponent> = {
  title: 'Board/MetricTable',
  component: MetricTableComponent,
};

export default meta;
type Story = StoryObj<MetricTableComponent>;

/** Populated community-metrics surface with the Status, Value, and Help columns. */
export const Default: Story = {
  args: { rows: COMMUNITY_METRICS, metricHead: 'Metric', valueHead: 'Value', showStatus: true },
};

/** Headline "hero statistics" surface — counts with no status column (Work Processed). */
export const HeroStatistics: Story = {
  args: { rows: HERO_STATS, metricHead: 'Work area', valueHead: 'Count', showStatus: false },
};

/** Empty state (FR-030): an explicit "no metrics yet" panel, not a headers-only table. */
export const Empty: Story = {
  args: { rows: [], metricHead: 'Metric', valueHead: 'Value', showStatus: true },
};

/** Per-row unavailable state (FR-036): a broken metric renders unavailable without blanking siblings. */
export const WithUnavailableRow: Story = {
  args: {
    rows: [
      { id: 'broken', label: 'External Balance Feed', definitionText: 'Balance pulled from the accounting system.', value: null, detail: null, status: 'Unavailable', emphasis: 'none' },
      ...COMMUNITY_METRICS,
    ],
    metricHead: 'Metric', valueHead: 'Value', showStatus: true,
  },
};
