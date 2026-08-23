import { render, screen, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { MetadataChange, PagedResult } from '../../types/api'
import ColumnHistoryPanel from './ColumnHistoryPanel'
import { getColumnHistory } from '../../services/metadataService'

vi.mock('../../services/metadataService', () => ({
  getColumnHistory: vi.fn(),
}))

function page(items: MetadataChange[], total = items.length): PagedResult<MetadataChange> {
  return { items, total, limit: 50, offset: 0 }
}

function renderPanel() {
  render(
    <ColumnHistoryPanel columnId="c1" columnName="orders.total_amount" onClose={vi.fn()} />,
  )
}

describe('ColumnHistoryPanel', () => {
  beforeEach(() => vi.clearAllMocks())

  it('names the column it is describing', async () => {
    vi.mocked(getColumnHistory).mockResolvedValue(page([]))

    renderPanel()

    expect(await screen.findByText('orders.total_amount')).toBeInTheDocument()
  })

  it('shows what changed, who changed it, and what it changed from', async () => {
    vi.mocked(getColumnHistory).mockResolvedValue(
      page([
        {
          id: 'h1',
          field: 'Owner',
          oldValue: 'ana',
          newValue: 'bob',
          editedByEmail: 'ana@example.com',
          editedAt: '2026-08-23T10:00:00Z',
        },
      ]),
    )

    renderPanel()

    expect(await screen.findByText('Owner')).toBeInTheDocument()
    expect(screen.getByText('ana')).toBeInTheDocument()
    expect(screen.getByText('bob')).toBeInTheDocument()
    expect(screen.getByText('ana@example.com')).toBeInTheDocument()
  })

  it('reads a field name the way the UI spells it, not the way the domain does', async () => {
    vi.mocked(getColumnHistory).mockResolvedValue(
      page([
        {
          id: 'h1',
          field: 'ExampleValue',
          oldValue: null,
          newValue: '12.50',
          editedByEmail: 'ana@example.com',
          editedAt: '2026-08-23T10:00:00Z',
        },
      ]),
    )

    renderPanel()

    expect(await screen.findByText('Example value')).toBeInTheDocument()
  })

  it('distinguishes a field that was empty from one that was cleared', async () => {
    vi.mocked(getColumnHistory).mockResolvedValue(
      page([
        {
          id: 'h1',
          field: 'Description',
          oldValue: 'was here',
          newValue: null,
          editedByEmail: 'ana@example.com',
          editedAt: '2026-08-23T10:00:00Z',
        },
        {
          id: 'h2',
          field: 'Description',
          oldValue: null,
          newValue: 'was here',
          editedByEmail: 'ana@example.com',
          editedAt: '2026-08-22T10:00:00Z',
        },
      ]),
    )

    renderPanel()

    expect(await screen.findByText('cleared')).toBeInTheDocument()
    expect(screen.getByText('empty')).toBeInTheDocument()
  })

  it('says so when the column has never been edited', async () => {
    vi.mocked(getColumnHistory).mockResolvedValue(page([]))

    renderPanel()

    await waitFor(() =>
      expect(screen.getByText(/has not been edited yet/)).toBeInTheDocument(),
    )
  })

  it('reports a failure to load rather than showing an empty history', async () => {
    vi.mocked(getColumnHistory).mockRejectedValue(new Error('Service unavailable'))

    renderPanel()

    // An empty panel here would read as "nobody has edited this", which is a different fact.
    expect(await screen.findByText('Service unavailable')).toBeInTheDocument()
    expect(screen.queryByText(/has not been edited yet/)).not.toBeInTheDocument()
  })

  it('says when it is showing only the most recent of many edits', async () => {
    vi.mocked(getColumnHistory).mockResolvedValue(
      page(
        [
          {
            id: 'h1',
            field: 'Owner',
            oldValue: 'ana',
            newValue: 'bob',
            editedByEmail: 'ana@example.com',
            editedAt: '2026-08-23T10:00:00Z',
          },
        ],
        120,
      ),
    )

    renderPanel()

    expect(await screen.findByText(/most recent of 120 edits/)).toBeInTheDocument()
  })
})
