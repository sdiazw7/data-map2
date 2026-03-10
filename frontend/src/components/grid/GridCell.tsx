import { useState, useRef, useEffect } from 'react'

type Props = {
  value: string | null
  onChange: (value: string) => void
}

export default function GridCell({ value, onChange }: Props) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(value ?? '')
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (editing) {
      inputRef.current?.focus()
      inputRef.current?.select()
    }
  }, [editing])

  function startEditing() {
    setDraft(value ?? '')
    setEditing(true)
  }

  function commit() {
    setEditing(false)
    if (draft !== (value ?? '')) {
      onChange(draft)
    }
  }

  function cancel() {
    setDraft(value ?? '')
    setEditing(false)
  }

  if (editing) {
    return (
      <input
        ref={inputRef}
        value={draft}
        onChange={e => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={e => {
          if (e.key === 'Enter') { e.preventDefault(); commit() }
          if (e.key === 'Escape') { e.preventDefault(); cancel() }
        }}
        className="w-full px-1 py-0.5 border border-blue-500 rounded outline-none text-sm"
      />
    )
  }

  return (
    <span
      role="button"
      tabIndex={0}
      onClick={startEditing}
      onKeyDown={e => { if (e.key === 'Enter') startEditing() }}
      className={`block w-full px-1 py-0.5 cursor-pointer text-sm ${
        value ? '' : 'text-gray-400 italic'
      }`}
    >
      {value || '—'}
    </span>
  )
}
