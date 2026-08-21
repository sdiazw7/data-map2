import { describe, it, expect } from 'vitest'
import type { ColumnGridRow } from '../types/api'
import { buildPasteEdits, parseClipboardGrid } from './clipboard'

describe('parseClipboardGrid', () => {
  it('reads a rectangle of tab-separated cells', () => {
    expect(parseClipboardGrid('a\tb\nc\td')).toEqual([
      ['a', 'b'],
      ['c', 'd'],
    ])
  })

  it('reads a single cell', () => {
    expect(parseClipboardGrid('just one')).toEqual([['just one']])
  })

  it('treats empty cells as cells rather than dropping them', () => {
    // A cleared cell in the middle of a pasted block still has to land, or every value to its
    // right shifts one column left.
    expect(parseClipboardGrid('a\t\tc')).toEqual([['a', '', 'c']])
  })

  it('accepts the CRLF line endings Excel puts on the clipboard', () => {
    expect(parseClipboardGrid('a\tb\r\nc\td')).toEqual([
      ['a', 'b'],
      ['c', 'd'],
    ])
  })

  it('ignores the trailing newline left by copying whole rows', () => {
    expect(parseClipboardGrid('a\nb\n')).toEqual([['a'], ['b']])
  })

  it('keeps a quoted cell whole when it contains a tab or a newline', () => {
    // Splitting on tabs and newlines alone would tear this cell into three.
    expect(parseClipboardGrid('"has\ttab"\tplain')).toEqual([['has\ttab', 'plain']])
    expect(parseClipboardGrid('"line\nbreak"\tplain')).toEqual([['line\nbreak', 'plain']])
  })

  it('unescapes the doubled quotes inside a quoted cell', () => {
    expect(parseClipboardGrid('"she said ""hi"""')).toEqual([['she said "hi"']])
  })

  it('leaves a quote that does not open the cell as literal text', () => {
    expect(parseClipboardGrid('5" pipe')).toEqual([['5" pipe']])
  })

  it('returns nothing for an empty payload', () => {
    expect(parseClipboardGrid('')).toEqual([])
  })
})

function makeRow(id: string): ColumnGridRow {
  return {
    columnId: id,
    schemaName: 'sales',
    tableName: 'orders',
    columnName: `col_${id}`,
    dataType: 'text',
    exampleValue: null,
    description: null,
    businessTerm: null,
    owner: null,
    version: 1,
  }
}

describe('buildPasteEdits', () => {
  const rows = [makeRow('c1'), makeRow('c2'), makeRow('c3')]

  it('spills right across the editable fields and down across rows', () => {
    const { edits, skippedRows } = buildPasteEdits(
      rows,
      { rowIndex: 0, field: 'description' },
      [
        ['first', 'ex1', 'ana'],
        ['second', 'ex2', 'bob'],
      ],
    )

    expect(skippedRows).toBe(0)
    expect(edits).toEqual([
      { columnId: 'c1', edit: { description: 'first', exampleValue: 'ex1', owner: 'ana' } },
      { columnId: 'c2', edit: { description: 'second', exampleValue: 'ex2', owner: 'bob' } },
    ])
  })

  it('starts at the anchored field rather than the first one', () => {
    const { edits } = buildPasteEdits(rows, { rowIndex: 1, field: 'exampleValue' }, [['ex', 'ana']])

    expect(edits).toEqual([{ columnId: 'c2', edit: { exampleValue: 'ex', owner: 'ana' } }])
  })

  it('drops cells that spill past the last editable field', () => {
    // Three fields, anchored on the last one: everything after the first cell has nowhere to go.
    const { edits } = buildPasteEdits(rows, { rowIndex: 0, field: 'owner' }, [['ana', 'over', 'run']])

    expect(edits).toEqual([{ columnId: 'c1', edit: { owner: 'ana' } }])
  })

  it('reports rows that fall past the end of the loaded window', () => {
    const { edits, skippedRows } = buildPasteEdits(
      rows,
      { rowIndex: 2, field: 'description' },
      [['a'], ['b'], ['c']],
    )

    // Only one row is left below the anchor, so two had nowhere to land.
    expect(edits).toEqual([{ columnId: 'c3', edit: { description: 'a' } }])
    expect(skippedRows).toBe(2)
  })

  it('writes an empty cell as null, so a cleared cell reads as undocumented', () => {
    const { edits } = buildPasteEdits(rows, { rowIndex: 0, field: 'description' }, [['', 'ex']])

    expect(edits).toEqual([{ columnId: 'c1', edit: { description: null, exampleValue: 'ex' } }])
  })
})
