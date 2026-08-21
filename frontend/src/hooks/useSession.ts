import { useState } from 'react'
import type { JoinResponse } from '../types/api'

const STORAGE_KEY = 'datamap_session'

/**
 * Reads and clears the stored session outside React. The 401 handler runs from the fetch
 * layer, where no hook is in scope, and it still has to drop a session the server has stopped
 * honouring.
 */
export function readStoredSession(): JoinResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    return JSON.parse(raw) as JoinResponse
  } catch {
    return null
  }
}

export function clearStoredSession(): void {
  localStorage.removeItem(STORAGE_KEY)
}

export function useSession(): {
  session: JoinResponse | null
  saveSession: (session: JoinResponse) => void
  clearSession: () => void
} {
  const [session, setSession] = useState<JoinResponse | null>(readStoredSession)

  function saveSession(s: JoinResponse): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(s))
    setSession(s)
  }

  function clearSession(): void {
    clearStoredSession()
    setSession(null)
  }

  return { session, saveSession, clearSession }
}
