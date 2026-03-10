import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import InvitePage from './components/pages/InvitePage'
import WorkspacePage from './components/pages/WorkspacePage'
import AppHeader from './components/ui/AppHeader'

export default function App() {
  return (
    <BrowserRouter>
      <div className="flex flex-col min-h-screen">
        <AppHeader />
        <Routes>
          <Route path="/" element={<Navigate to="/workspace" replace />} />
          <Route path="/invite/:token" element={<InvitePage />} />
          <Route path="/workspace" element={<WorkspacePage />} />
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
