import { waitFor } from '@testing-library/angular';

/**
 * Test helper: Jasmine's `expect` records a failure without throwing, so `waitFor(() => expect(...))`
 * returns on its first attempt. `until` throws until the condition holds, so it really waits.
 */
export function until(condition: () => boolean, timeout = 3000): Promise<void> {
  return waitFor(() => {
    if (!condition()) throw new Error('condition not met yet');
  }, { timeout });
}
