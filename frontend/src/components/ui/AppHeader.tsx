import { Link } from 'react-router-dom'
import { useSession } from '../../hooks/useSession'

export default function AppHeader() {
  const { session } = useSession()

  return (
    <header className="flex items-center justify-between px-4 py-3 border-b border-gray-200 bg-white shrink-0">
      <div className="flex items-center gap-3">
        <span className="text-lg font-semibold text-gray-900">DataMap</span>
        {session && (
          <span className="text-sm text-gray-500">{session.workspaceName}</span>
        )}
      </div>
      <div className="flex items-center gap-4">
        {import.meta.env.DEV && (
          <Link to="/" className="text-sm text-gray-500 hover:text-gray-700">
            Switch workspace
          </Link>
        )}
        <a
          href="/csv-guide"
          target="_blank"
          rel="noopener noreferrer"
          className="text-sm text-gray-500 hover:text-gray-700"
        >
          CSV Upload Guide
        </a>
      </div>
    </header>
  )
}
