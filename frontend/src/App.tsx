import { useEffect } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { setUnauthorizedHandler } from './utils/api'
import { clearStoredSession, readStoredSession } from './hooks/useSession'
import InvitePage from './components/pages/InvitePage'
import WorkspacePage from './components/pages/WorkspacePage'
import WorkspacePickerPage from './components/pages/WorkspacePickerPage'
import CsvUploadGuidePage from './components/pages/CsvUploadGuidePage'
import AppHeader from './components/ui/AppHeader'

export default function App() {
  useEffect(() => {
    setUnauthorizedHandler(() => {
      // The session cookie expires well before the copy in localStorage does, leaving the UI
      // sitting in a workspace every later call 401s against. Drop the stored copy and start
      // over, which also resets the per-component session state the app keeps in several
      // places.
      //
      // Guarded on there being a session to drop: the workspace route mounts its data hooks
      // before it checks for one, so an unauthenticated visit 401s on its own and would
      // otherwise redirect here on every response.
      if (!readStoredSession()) return

      clearStoredSession()
      window.location.assign('/')
    })
    return () => setUnauthorizedHandler(null)
  }, [])

  return (
    <BrowserRouter>
      <div className="flex flex-col min-h-screen">
        <AppHeader />
        <Routes>
          <Route
            path="/"
            element={import.meta.env.DEV ? <WorkspacePickerPage /> : <Navigate to="/workspace" replace />}
          />
          <Route path="/invite/:token" element={<InvitePage />} />
          <Route path="/workspace" element={<WorkspacePage />} />
          <Route path="/csv-guide" element={<CsvUploadGuidePage />} />
          <Route path="*" element={
            <div className="flex items-center justify-center flex-1">
              <p className="text-gray-600">Page not found. Check your URL or use your invite link to get started.</p>
            </div>
          } />
        </Routes>
      </div>
    </BrowserRouter>
  )
}
