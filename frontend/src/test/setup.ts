import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Vitest runs without injected globals here, so Testing Library's own auto-cleanup never
// registers. Without this each render is left in the document and the next test's queries
// find several copies of the component.
afterEach(cleanup)
