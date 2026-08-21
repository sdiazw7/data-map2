import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ColumnGridRow } from '../../types/api'
import MetadataGrid from './MetadataGrid'

// jsdom gives the scroll container no height, so the real virtualizer reports nothing on
// screen and the grid renders no rows at all. This stands in for it by putting every row in
// view, which is what the assertions below are about — the wiring, not the windowing.
vi.mock('@tanstack/react-virtual', () => ({
  useVirtualizer: ({ count }: { count: number }) => ({
    getVirtualItems: () =>
      Array.from({ length: count }, (_, index) => ({
        index,
        key: index,
        start: index * 36,
        end: (index + 1) * 36,
        size: 36,
      })),
    getTotalSize: () => count * 36,
    scrollToIndex: vi.fn(),
  }),
}))

function makeRow(id: string, description: string): ColumnGridRow {
  return {
    columnId: id,
    schemaName: 'sales',
    tableName: 'orders',
    columnName: `col_${id}`,
    dataType: 'text',
    exampleValue: 'ex',
    description,
    businessTerm: null,
    owner: 'ana',
    version: 1,
  }
}

const rows = [makeRow('c1', 'First'), makeRow('c2', 'Second'), makeRow('c3', 'Third')]

let onEdit: ReturnType<typeof vi.fn>
let onPasteEdits: ReturnType<typeof vi.fn>

function renderGrid() {
  onEdit = vi.fn()
  onPasteEdits = vi.fn()

  render(
    <MetadataGrid
      columns={rows}
      terms={[]}
      onEdit={onEdit}
      total={rows.length}
      onLoadMore={vi.fn()}
      isLoadingMore={false}
      onTermMap={vi.fn()}
      onPasteEdits={onPasteEdits}
      sortBy="columnName"
      sortDir="asc"
      onSortChange={vi.fn()}
    />,
  )

  return screen.getByRole('grid')
}

/** The cell showing the given text. Keyboard events are fired on the grid, as the browser does. */
function cell(text: string) {
  return screen.getByText(text)
}

describe('MetadataGrid keyboard editing', () => {
  beforeEach(() => vi.clearAllMocks())

  it('marks the clicked cell as the selected one', () => {
    renderGrid()

    fireEvent.click(cell('First'))

    const selected = screen.getAllByRole('gridcell').filter(c => c.getAttribute('aria-selected') === 'true')
    expect(selected).toHaveLength(1)
    expect(selected[0]).toHaveTextContent('First')
  })

  it('moves the selection down a column with the arrow keys', () => {
    const grid = renderGrid()

    fireEvent.click(cell('First'))
    fireEvent.keyDown(grid, { key: 'ArrowDown' })

    const selected = screen.getAllByRole('gridcell').filter(c => c.getAttribute('aria-selected') === 'true')
    expect(selected[0]).toHaveTextContent('Second')
  })

  it('opens an editor seeded with the character typed over the cell', () => {
    const grid = renderGrid()

    fireEvent.click(cell('First'))
    fireEvent.keyDown(grid, { key: 'X' })

    // The keystroke that opened the editor is not swallowed.
    expect(screen.getByRole('textbox')).toHaveValue('X')
  })

  it('writes the edit on Enter and steps down to the next row', () => {
    const grid = renderGrid()

    fireEvent.click(cell('First'))
    fireEvent.keyDown(grid, { key: 'Enter' })

    const input = screen.getByRole('textbox')
    fireEvent.change(input, { target: { value: 'Rewritten' } })
    fireEvent.keyDown(input, { key: 'Enter' })

    expect(onEdit).toHaveBeenCalledWith('c1', { description: 'Rewritten' })

    const selected = screen.getAllByRole('gridcell').filter(c => c.getAttribute('aria-selected') === 'true')
    expect(selected[0]).toHaveTextContent('Second')
  })

  it('writes nothing when the editor closes on an unchanged value', () => {
    const grid = renderGrid()

    fireEvent.click(cell('First'))
    fireEvent.keyDown(grid, { key: 'Enter' })
    fireEvent.keyDown(screen.getByRole('textbox'), { key: 'Enter' })

    // Tabbing through a column would otherwise write every cell it passed through.
    expect(onEdit).not.toHaveBeenCalled()
  })

  it('abandons the edit on Escape', () => {
    const grid = renderGrid()

    fireEvent.click(cell('First'))
    fireEvent.keyDown(grid, { key: 'Enter' })

    const input = screen.getByRole('textbox')
    fireEvent.change(input, { target: { value: 'Discarded' } })
    fireEvent.keyDown(input, { key: 'Escape' })

    expect(onEdit).not.toHaveBeenCalled()
    expect(cell('First')).toBeInTheDocument()
  })

  it('clears the cell through the editor on Delete', () => {
    const grid = renderGrid()

    fireEvent.click(cell('First'))
    fireEvent.keyDown(grid, { key: 'Delete' })

    expect(screen.getByRole('textbox')).toHaveValue('')

    fireEvent.keyDown(screen.getByRole('textbox'), { key: 'Enter' })
    expect(onEdit).toHaveBeenCalledWith('c1', { description: null })
  })
})

describe('MetadataGrid paste', () => {
  beforeEach(() => vi.clearAllMocks())

  it('lays a pasted block over the rows from the selected cell', () => {
    const grid = renderGrid()

    fireEvent.click(cell('Second'))
    fireEvent.paste(grid, {
      clipboardData: { getData: () => 'desc two\tex two\nDesc three\tex three' },
    })

    expect(onPasteEdits).toHaveBeenCalledWith(
      [
        { columnId: 'c2', edit: { description: 'desc two', exampleValue: 'ex two' } },
        { columnId: 'c3', edit: { description: 'Desc three', exampleValue: 'ex three' } },
      ],
      0,
    )
  })

  it('reports the pasted rows that ran off the end of the loaded window', () => {
    const grid = renderGrid()

    fireEvent.click(cell('Third'))
    fireEvent.paste(grid, { clipboardData: { getData: () => 'a\nb\nc' } })

    const [, skippedRows] = onPasteEdits.mock.calls[0]
    expect(skippedRows).toBe(2)
  })

  it('leaves the paste to the editor when one is open', () => {
    const grid = renderGrid()

    fireEvent.click(cell('First'))
    fireEvent.keyDown(grid, { key: 'Enter' })
    fireEvent.paste(grid, { clipboardData: { getData: () => 'a\tb' } })

    // Pasting into an open editor is an ordinary text paste, not a range.
    expect(onPasteEdits).not.toHaveBeenCalled()
  })
})
