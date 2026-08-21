import { renderHook, act } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import type { KeyboardEvent } from 'react'
import { useGridSelection } from './useGridSelection'

/** A keydown standing in for the React synthetic event the grid hands over. */
function key(k: string, modifiers: Partial<KeyboardEvent> = {}): KeyboardEvent {
  return {
    key: k,
    shiftKey: false,
    ctrlKey: false,
    metaKey: false,
    altKey: false,
    preventDefault: vi.fn(),
    ...modifiers,
  } as unknown as KeyboardEvent
}

function renderSelection(rowCount = 3) {
  return renderHook(() => useGridSelection({ rowCount }))
}

describe('useGridSelection', () => {
  it('starts with nothing selected and consumes no keys', () => {
    const { result } = renderSelection()

    expect(result.current.active).toBeNull()

    let consumed = true
    act(() => {
      consumed = result.current.handleKeyDown(key('ArrowDown'))
    })
    expect(consumed).toBe(false)
  })

  it('moves between rows and fields with the arrow keys', () => {
    const { result } = renderSelection()

    act(() => result.current.activate({ rowIndex: 0, field: 'description' }))

    act(() => {
      result.current.handleKeyDown(key('ArrowRight'))
    })
    expect(result.current.active).toEqual({ rowIndex: 0, field: 'exampleValue' })

    act(() => {
      result.current.handleKeyDown(key('ArrowDown'))
    })
    expect(result.current.active).toEqual({ rowIndex: 1, field: 'exampleValue' })

    act(() => {
      result.current.handleKeyDown(key('ArrowLeft'))
    })
    expect(result.current.active).toEqual({ rowIndex: 1, field: 'description' })
  })

  it('wraps to the next row when tabbing off the last field', () => {
    const { result } = renderSelection()

    act(() => result.current.activate({ rowIndex: 0, field: 'owner' }))

    act(() => {
      result.current.handleKeyDown(key('Tab'))
    })
    expect(result.current.active).toEqual({ rowIndex: 1, field: 'description' })

    // And back the other way, which is how a user walks a correction backwards.
    act(() => {
      result.current.handleKeyDown(key('Tab', { shiftKey: true }))
    })
    expect(result.current.active).toEqual({ rowIndex: 0, field: 'owner' })
  })

  it('stops at the edges of the loaded window', () => {
    const { result } = renderSelection(2)

    act(() => result.current.activate({ rowIndex: 0, field: 'description' }))
    act(() => {
      result.current.handleKeyDown(key('ArrowUp'))
    })
    expect(result.current.active).toEqual({ rowIndex: 0, field: 'description' })

    act(() => result.current.activate({ rowIndex: 1, field: 'owner' }))
    act(() => {
      result.current.handleKeyDown(key('ArrowDown'))
    })
    expect(result.current.active).toEqual({ rowIndex: 1, field: 'owner' })
  })

  it('opens the editor on Enter, on the cell as it stands', () => {
    const { result } = renderSelection()

    act(() => result.current.activate({ rowIndex: 0, field: 'description' }))
    act(() => {
      result.current.handleKeyDown(key('Enter'))
    })

    expect(result.current.isEditing).toBe(true)
    expect(result.current.editSeed).toBeNull()
  })

  it('opens the editor on a printable key and keeps the character that opened it', () => {
    const { result } = renderSelection()

    act(() => result.current.activate({ rowIndex: 0, field: 'description' }))
    act(() => {
      result.current.handleKeyDown(key('x'))
    })

    // Without the seed the first keystroke of every typed-over cell would be swallowed.
    expect(result.current.isEditing).toBe(true)
    expect(result.current.editSeed).toBe('x')
  })

  it('leaves shortcuts alone rather than treating them as typing', () => {
    const { result } = renderSelection()

    act(() => result.current.activate({ rowIndex: 0, field: 'description' }))

    let consumed = true
    act(() => {
      consumed = result.current.handleKeyDown(key('v', { ctrlKey: true }))
    })

    // Ctrl+V has to reach the paste handler.
    expect(consumed).toBe(false)
    expect(result.current.isEditing).toBe(false)
  })

  it('opens the editor empty on Delete, so the cell can be cleared', () => {
    const { result } = renderSelection()

    act(() => result.current.activate({ rowIndex: 0, field: 'exampleValue' }))
    act(() => {
      result.current.handleKeyDown(key('Delete'))
    })

    expect(result.current.isEditing).toBe(true)
    expect(result.current.editSeed).toBe('')
  })

  it('ignores the grid keys while the editor is open, which owns them', () => {
    const { result } = renderSelection()

    act(() => result.current.activate({ rowIndex: 0, field: 'description' }))
    act(() => result.current.startEdit())

    let consumed = true
    act(() => {
      consumed = result.current.handleKeyDown(key('ArrowDown'))
    })

    expect(consumed).toBe(false)
    expect(result.current.active).toEqual({ rowIndex: 0, field: 'description' })
  })

  it('steps on the way out of the editor, so Enter advances down the column', () => {
    const { result } = renderSelection()

    act(() => result.current.activate({ rowIndex: 0, field: 'description' }))
    act(() => result.current.startEdit())
    act(() => result.current.stopEdit('down'))

    expect(result.current.isEditing).toBe(false)
    expect(result.current.active).toEqual({ rowIndex: 1, field: 'description' })
  })

  it('jumps to the ends of the row with Home and End', () => {
    const { result } = renderSelection()

    act(() => result.current.activate({ rowIndex: 1, field: 'exampleValue' }))

    act(() => {
      result.current.handleKeyDown(key('End'))
    })
    expect(result.current.active).toEqual({ rowIndex: 1, field: 'owner' })

    act(() => {
      result.current.handleKeyDown(key('Home'))
    })
    expect(result.current.active).toEqual({ rowIndex: 1, field: 'description' })
  })
})
