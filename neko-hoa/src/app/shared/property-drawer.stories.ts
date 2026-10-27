import type { Meta, StoryObj } from '@storybook/angular';
import { moduleMetadata } from '@storybook/angular';
import { PropertyDrawerComponent } from './property-drawer.component';
import { storyAt } from '../../storybook/variants';

// 030 T149 — brief 06 8.06.y: the property drawer beside the list; full width below 768 px.
const meta: Meta<PropertyDrawerComponent> = {
  title: 'Shared/PropertyDrawer',
  component: PropertyDrawerComponent,
  decorators: [moduleMetadata({ imports: [PropertyDrawerComponent] })],
  render: args => ({
    props: args,
    template: `
      <div style="display: flex; gap: 12px; min-height: 420px;">
        <div class="card" style="flex: 1;"><p class="muted">The list stays visible behind the drawer.</p></div>
        <app-property-drawer [heading]="heading">
          <h3 class="section-title">Residents</h3>
          <p><b>Jane Alvarez</b> · jane&#64;example.com · Resident since Mar 2, 2024</p>
          <h3 class="section-title">Invitations</h3>
          <p>kim&#64;example.com <span class="pill">Pending</span></p>
          <button type="button" class="btn btn--primary">Invite resident</button>
        </app-property-drawer>
      </div>`,
  }),
};
export default meta;
type Story = StoryObj<PropertyDrawerComponent>;
const at = storyAt<PropertyDrawerComponent>();

const base: Story = { args: { heading: '711 Keystone Park Dr #29' } };

export const Desktop: Story = at('desktop', base);
export const DesktopDark: Story = at('desktop', base, true);
export const Tablet: Story = at('tablet', base);
export const Phone: Story = at('phone', base);
export const PhoneDark: Story = at('phone', base, true);
