import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import InvitePage from './components/pages/InvitePage'
import WorkspacePage from './components/pages/WorkspacePage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/workspace" replace />} />
        <Route path="/invite/:token" element={<InvitePage />} />
        <Route path="/workspace" element={<WorkspacePage />} />
      </Routes>
    </BrowserRouter>
  )
}
