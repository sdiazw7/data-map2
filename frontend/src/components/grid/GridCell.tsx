import { useState, useRef, useEffect } from 'react'

/** Where the selection goes when an edit is committed from the keyboard. */
export type CommitMove = 'down' | 'right' | 'left' | 'none'

type Props = {
  value: string | null
  isActive: boolean
  isEditing: boolean
  /**
   * Seeds the editor when editing began by typing over the cell: the character typed, or an
   * empty string for a cleared cell. Null when editing began from Enter, F2 or a double click,
   * where the editor opens on the existing value instead.
   */
  editSeed: string | null
  onActivate: () => void
  onStartEdit: () => void
  onCommit: (value: string, move: CommitMove) => void
  /** Left the editor without a change to write; still steps, so Enter always advances. */
  onCancel: (move: CommitMove) => void
}

export default function GridCell({
  value,
  isActive,
  isEditing,
  editSeed,
  onActivate,
  onStartEdit,
  onCommit,
  onCancel,
}: Props) {
  const [draft, setDraft] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  // The editor is opened by the grid, so the draft is seeded here rather than in a click
  // handler. Typing over a cell replaces it and the caret follows the character just typed;
  // opening it any other way offers the existing value, selected so that typing replaces it.
  useEffect(() => {
    if (!isEditing) return

    setDraft(editSeed !== null ? editSeed : value ?? '')

    const input = inputRef.current
    if (!input) return

    input.focus()
    if (editSeed === null) input.select()
    // Only on entering edit mode: re-seeding on every keystroke would fight the input.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isEditing])

  function commit(move: CommitMove) {
    // Unchanged values are not written. A grid edit costs a request and a version, and tabbing
    // through a column would otherwise write every cell it passed through. The move still
    // happens either way, so Enter advances whether or not anything was typed.
    if (draft !== (value ?? '')) {
      onCommit(draft, move)
    } else {
      onCancel(move)
    }
  }

  if (isEditing) {
    return (
      <input
        ref={inputRef}
        value={draft}
        onChange={e => setDraft(e.target.value)}
        onBlur={() => commit('none')}
        onKeyDown={e => {
          // The editor owns the keyboard while it is open, so these do not reach the grid.
          if (e.key === 'Enter') {
            e.preventDefault()
            commit('down')
          }
          if (e.key === 'Escape') {
            e.preventDefault()
            onCancel('none')
          }
          if (e.key === 'Tab') {
            e.preventDefault()
            commit(e.shiftKey ? 'left' : 'right')
          }
        }}
        className="w-full px-1 py-0.5 border border-blue-500 rounded outline-none text-sm"
      />
    )
  }

  return (
    <span
      onClick={onActivate}
      onDoubleClick={onStartEdit}
      className={`block w-full px-1 py-0.5 cursor-cell text-sm rounded ${
        isActive ? 'ring-2 ring-inset ring-blue-500' : ''
      } ${value ? '' : 'text-gray-400 italic'}`}
    >
      {value || '—'}
    </span>
  )
}
