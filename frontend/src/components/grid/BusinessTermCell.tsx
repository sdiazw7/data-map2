import type { BusinessTermDto } from '../../types/api'

type Props = {
  value: string | null
  terms: BusinessTermDto[]
  onChange: (termId: string) => void
}

export default function BusinessTermCell({ value, terms, onChange }: Props) {
  const selectedTerm = terms.find(t => t.name === value)
  const selectedId = selectedTerm?.id ?? ''

  return (
    <select
      value={selectedId}
      onChange={e => onChange(e.target.value)}
      className="w-full text-sm border-none bg-transparent cursor-pointer focus:outline-none focus:ring-1 focus:ring-blue-500 rounded px-1 py-0.5"
    >
      <option value="">—</option>
      {terms.map(term => (
        <option key={term.id} value={term.id}>
          {term.name}
        </option>
      ))}
    </select>
  )
}
