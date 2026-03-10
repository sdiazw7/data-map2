const BASE_URL = import.meta.env.VITE_API_BASE_URL

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })
  if (!res.ok) {
    const body = await res.json().catch(() => null)
    throw new Error(body?.error?.message ?? `Request failed: ${res.status}`)
  }
  const text = await res.text()
  return text ? (JSON.parse(text) as T) : (undefined as T)
}
