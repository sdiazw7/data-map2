import { useCallback, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import type { CommitMove } from '../components/grid/GridCell'
import type { EditableField } from '../utils/columnFields'
import { EDITABLE_FIELDS } from '../utils/columnFields'

/** The cell the grid is on, by position in the loaded window and by field. */
export type CellRef = { rowIndex: number; field: EditableField }

type UseGridSelectionOptions = {
  /** Rows currently loaded. Movement stops at the last one rather than running off the end. */
  rowCount: number
}

type UseGridSelectionResult = {
  active: CellRef | null
  isEditing: boolean
  /**
   * The character that opened the editor when editing began by typing, so the keystroke that
   * started it is not swallowed. Null when editing began from Enter, F2 or a click.
   */
  editSeed: string | null
  activate: (cell: CellRef) => void
  startEdit: () => void
  /** Leaves the editor and, optionally, steps on the way out. */
  stopEdit: (move?: CommitMove) => void
  /** Handles the grid's own keys. Returns true when it consumed the event. */
  handleKeyDown: (event: KeyboardEvent) => boolean
}

/** Whether a keypress is a character that should open the editor and land inside it. */
function isTypingKey(event: KeyboardEvent): boolean {
  return event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey
}

export function useGridSelection({ rowCount }: UseGridSelectionOptions): UseGridSelectionResult {
  const [active, setActive] = useState<CellRef | null>(null)
  const [isEditing, setIsEditing] = useState(false)
  const [editSeed, setEditSeed] = useState<string | null>(null)

  // Movement reads the current cell, and a key handler runs a render behind the state it set.
  const activeRef = useRef<CellRef | null>(null)
  activeRef.current = active

  const rowCountRef = useRef(rowCount)
  rowCountRef.current = rowCount

  const activate = useCallback((cell: CellRef) => {
    // Kept in step with the ref so that a double click, which activates and then opens the
    // editor in the same tick, does not read the cell it replaced.
    activeRef.current = cell
    setActive(cell)
    setIsEditing(false)
    setEditSeed(null)
  }, [])

  const startEdit = useCallback(() => {
    if (!activeRef.current) return
    setEditSeed(null)
    setIsEditing(true)
  }, [])

  const move = useCallback((rowStep: number, fieldStep: number) => {
    const current = activeRef.current
    if (!current) return

    const fieldIndex = EDITABLE_FIELDS.indexOf(current.field) + fieldStep
    let rowIndex = current.rowIndex + rowStep
    let field = current.field

    if (fieldIndex < 0) {
      // Off the left edge wraps to the last field of the row above, the way Shift+Tab does in
      // a spreadsheet. Off the right edge wraps to the first field of the row below.
      rowIndex -= 1
      field = EDITABLE_FIELDS[EDITABLE_FIELDS.length - 1]
    } else if (fieldIndex >= EDITABLE_FIELDS.length) {
      rowIndex += 1
      field = EDITABLE_FIELDS[0]
    } else {
      field = EDITABLE_FIELDS[fieldIndex]
    }

    // Stop at the ends rather than wrapping past them: the window is a slice of a much larger
    // catalog, so running off the bottom has nowhere meaningful to land.
    if (rowIndex < 0 || rowIndex >= rowCountRef.current) return

    activeRef.current = { rowIndex, field }
    setActive({ rowIndex, field })
    setIsEditing(false)
    setEditSeed(null)
  }, [])

  const stopEdit = useCallback(
    (moveTo: CommitMove = 'none') => {
      setIsEditing(false)
      setEditSeed(null)

      if (moveTo === 'down') move(1, 0)
      if (moveTo === 'right') move(0, 1)
      if (moveTo === 'left') move(0, -1)
    },
    [move],
  )

  const handleKeyDown = useCallback(
    (event: KeyboardEvent): boolean => {
      const current = activeRef.current

      // While the editor is open it owns the keyboard; it reports back through stopEdit.
      if (!current || isEditing) return false

      switch (event.key) {
        case 'ArrowDown':
          move(1, 0)
          return true
        case 'ArrowUp':
          move(-1, 0)
          return true
        case 'ArrowRight':
          move(0, 1)
          return true
        case 'ArrowLeft':
          move(0, -1)
          return true
        case 'Tab':
          move(0, event.shiftKey ? -1 : 1)
          return true
        case 'Enter':
        case 'F2':
          startEdit()
          return true
        case 'Backspace':
        case 'Delete':
          // Clearing is an edit of its own; the grid commits an empty value for it.
          setEditSeed('')
          setIsEditing(true)
          return true
        case 'Home':
          setActive({ rowIndex: current.rowIndex, field: EDITABLE_FIELDS[0] })
          return true
        case 'End':
          setActive({
            rowIndex: current.rowIndex,
            field: EDITABLE_FIELDS[EDITABLE_FIELDS.length - 1],
          })
          return true
        case 'Escape':
          return false
        default:
          break
      }

      // Typing over a cell replaces it, and the character that started it belongs in the editor.
      if (isTypingKey(event)) {
        setEditSeed(event.key)
        setIsEditing(true)
        return true
      }

      return false
    },
    [isEditing, move, startEdit],
  )

  return { active, isEditing, editSeed, activate, startEdit, stopEdit, handleKeyDown }
}
