import { useState } from 'react'
import type { JoinResponse } from '../types/api'

const STORAGE_KEY = 'datamap_session'

export function useSession(): {
  session: JoinResponse | null
  saveSession: (session: JoinResponse) => void
  clearSession: () => void
} {
  const [session, setSession] = useState<JoinResponse | null>(() => {
    try {
      const raw = localStorage.getItem(STORAGE_KEY)
      if (!raw) return null
      return JSON.parse(raw) as JoinResponse
    } catch {
      return null
    }
  })

  function saveSession(s: JoinResponse): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(s))
    setSession(s)
  }

  function clearSession(): void {
    localStorage.removeItem(STORAGE_KEY)
    setSession(null)
  }

  return { session, saveSession, clearSession }
}
