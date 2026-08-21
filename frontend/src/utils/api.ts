import type { ApiErrorResponse } from '../types/api'

const BASE_URL = import.meta.env.VITE_API_BASE_URL

/** The error codes the client branches on. The API sends many more; these are the ones we act on. */
export const ApiErrorCode = {
  VersionConflict: 'VERSION_CONFLICT',
  Unauthorized: 'UNAUTHORIZED',
  SessionExpired: 'SESSION_EXPIRED',
  /** Stands in when a failure carried no envelope at all — a gateway error, a dropped connection. */
  Unknown: 'UNKNOWN',
} as const

/**
 * A failed API call, carrying the status and the code from the error envelope. The API
 * distinguishes a stale write from an expired session from a bad request, and callers have to
 * be able to tell them apart — a bare Error message can only ever be printed.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null

/**
 * Registers the app's response to a 401. Session expiry can surface on any call, so handling it
 * at each call site would mean repeating it at every one of them — and missing some.
 */
export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler
}

/**
 * Builds the error for a failed response. Exported because the CSV upload issues its own
 * request and has to fail the same way this one does.
 */
export async function toApiError(res: Response): Promise<ApiError> {
  const body = (await res.json().catch(() => null)) as ApiErrorResponse | null

  const error = new ApiError(
    res.status,
    body?.error?.code ?? ApiErrorCode.Unknown,
    body?.error?.message ?? `Request failed: ${res.status}`,
  )

  if (error.status === 401) unauthorizedHandler?.()

  return error
}

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
    throw await toApiError(res)
  }
  const text = await res.text()
  return text ? (JSON.parse(text) as T) : (undefined as T)
}
