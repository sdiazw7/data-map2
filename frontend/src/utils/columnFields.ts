import type { ColumnGridRow } from '../types/api'

/**
 * The row's editable fields, in the order the grid shows them. That order is what the grid
 * navigates along and what a pasted range spills across, so it is defined once here rather
 * than restated by each of them.
 */
export const EDITABLE_FIELDS = ['description', 'exampleValue', 'owner'] as const

export type EditableField = (typeof EDITABLE_FIELDS)[number]

/** The editable fields of a row. A cell sends only the one it changed. */
export type ColumnEdit = Partial<Pick<ColumnGridRow, EditableField>>

/** One edit per row. Several for the same row are merged into one write. */
export type ColumnEdits = { columnId: string; edit: ColumnEdit }[]

export function touchedFields(edit: ColumnEdit): EditableField[] {
  return EDITABLE_FIELDS.filter(field => field in edit)
}
