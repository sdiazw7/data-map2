import type { ColumnGridRow } from '../types/api'
import type { ColumnEdit, ColumnEdits, EditableField } from './columnFields'
import { EDITABLE_FIELDS } from './columnFields'

/**
 * Parses a clipboard payload copied out of a spreadsheet. Sheets and Excel put tab-separated
 * text on the clipboard, quoting any cell that holds a tab, a newline or a quote, and doubling
 * the quotes inside it — so splitting on tabs and newlines alone tears those cells apart.
 */
export function parseClipboardGrid(text: string): string[][] {
  if (text === '') return []

  const rows: string[][] = []
  let row: string[] = []
  let cell = ''
  let quoted = false
  let i = 0

  while (i < text.length) {
    const char = text[i]

    if (quoted) {
      if (char === '"') {
        // A doubled quote inside a quoted cell is one literal quote.
        if (text[i + 1] === '"') {
          cell += '"'
          i += 2
          continue
        }
        quoted = false
        i++
        continue
      }
      cell += char
      i++
      continue
    }

    // Only a quote that opens the cell quotes it; one partway through is literal text.
    if (char === '"' && cell === '') {
      quoted = true
      i++
      continue
    }

    if (char === '\t') {
      row.push(cell)
      cell = ''
      i++
      continue
    }

    if (char === '\r' || char === '\n') {
      if (char === '\r' && text[i + 1] === '\n') i++
      row.push(cell)
      rows.push(row)
      row = []
      cell = ''
      i++
      continue
    }

    cell += char
    i++
  }

  row.push(cell)
  rows.push(row)

  // Copying whole rows leaves a trailing newline, and that is a terminator rather than an
  // empty row of data.
  const last = rows[rows.length - 1]
  if (rows.length > 1 && last.length === 1 && last[0] === '') rows.pop()

  return rows
}

/** Where a paste starts: the row it lands on and the field the leftmost column writes to. */
export type PasteAnchor = { rowIndex: number; field: EditableField }

export type PasteResult = {
  edits: ColumnEdits
  /** Pasted rows that fell past the end of the loaded window and were not applied. */
  skippedRows: number
}

/**
 * Lays a pasted grid over the loaded rows from the anchor, spilling right across the editable
 * fields and down across rows. Cells that fall past the last editable field, or past the last
 * loaded row, have nowhere to go and are reported rather than applied.
 */
export function buildPasteEdits(
  rows: ColumnGridRow[],
  anchor: PasteAnchor,
  grid: string[][],
): PasteResult {
  const startField = EDITABLE_FIELDS.indexOf(anchor.field)
  const edits: ColumnEdits = []
  let skippedRows = 0

  grid.forEach((cells, rowOffset) => {
    const row = rows[anchor.rowIndex + rowOffset]
    if (!row) {
      skippedRows++
      return
    }

    const edit: ColumnEdit = {}

    cells.forEach((value, cellOffset) => {
      const field = EDITABLE_FIELDS[startField + cellOffset]
      if (!field) return

      // An empty cell clears the field. The grid stores an absent value as null, not as an
      // empty string, so that a cleared cell reads as undocumented everywhere it is counted.
      edit[field] = value === '' ? null : value
    })

    if (Object.keys(edit).length > 0) edits.push({ columnId: row.columnId, edit })
  })

  return { edits, skippedRows }
}
