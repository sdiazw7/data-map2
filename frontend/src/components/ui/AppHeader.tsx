export default function AppHeader() {
  return (
    <header className="flex items-center justify-between px-4 py-3 border-b border-gray-200 bg-white shrink-0">
      <span className="text-lg font-semibold text-gray-900">DataMap</span>
      <a
        href="/csv-guide"
        target="_blank"
        rel="noopener noreferrer"
        className="text-sm text-gray-500 hover:text-gray-700"
      >
        CSV Upload Guide
      </a>
    </header>
  )
}
