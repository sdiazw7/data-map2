import type { CoverageResponse } from '../../types/api'

type Props = {
  coverage: CoverageResponse
}

export default function CoverageBanner({ coverage }: Props) {
  const { documentedColumns, totalColumns, coveragePercent } = coverage
  const percent = Math.round(coveragePercent)

  return (
    <div className="bg-blue-600 text-white px-4 py-2">
      <div className="flex items-center justify-between mb-1">
        <span className="text-sm font-medium">
          {documentedColumns}/{totalColumns} columns documented ({percent}%)
        </span>
      </div>
      <div className="w-full bg-blue-400 rounded-full h-1.5">
        <div
          className="bg-white rounded-full h-1.5 transition-all duration-300"
          style={{ width: `${Math.min(percent, 100)}%` }}
        />
      </div>
    </div>
  )
}
