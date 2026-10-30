import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeEach, vi } from 'vitest';

afterEach(() => cleanup());

// No API in unit tests unless a test provides one: every request fails like an offline API.
beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new TypeError('No API in tests'))));
});
afterEach(() => vi.unstubAllGlobals());
