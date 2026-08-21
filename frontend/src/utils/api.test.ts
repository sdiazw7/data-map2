import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ApiError, ApiErrorCode, apiFetch, setUnauthorizedHandler } from './api'

function jsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  } as Response
}

describe('apiFetch', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    setUnauthorizedHandler(null)
    vi.unstubAllGlobals()
  })

  it('throws an ApiError carrying the status and the code from the envelope', async () => {
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse(409, { error: { code: 'VERSION_CONFLICT', message: 'Stale version.' } }),
    )

    const error = await apiFetch('/columns').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(409)
    expect((error as ApiError).code).toBe(ApiErrorCode.VersionConflict)
    expect((error as ApiError).message).toBe('Stale version.')
  })

  it('falls back to a synthetic code when the failure carries no envelope', async () => {
    vi.mocked(fetch).mockResolvedValue({
      ok: false,
      status: 502,
      json: () => Promise.reject(new Error('not json')),
    } as unknown as Response)

    const error = (await apiFetch('/columns').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(502)
    expect(error.code).toBe(ApiErrorCode.Unknown)
    expect(error.message).toContain('502')
  })

  it('notifies the unauthorized handler on a 401', async () => {
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)

    vi.mocked(fetch).mockResolvedValue(
      jsonResponse(401, { error: { code: 'SESSION_EXPIRED', message: 'Your session has expired.' } }),
    )

    const error = (await apiFetch('/columns').catch((e: unknown) => e)) as ApiError

    expect(onUnauthorized).toHaveBeenCalledOnce()
    expect(error.code).toBe(ApiErrorCode.SessionExpired)
  })

  it('leaves the unauthorized handler alone for other failures', async () => {
    const onUnauthorized = vi.fn()
    setUnauthorizedHandler(onUnauthorized)

    vi.mocked(fetch).mockResolvedValue(
      jsonResponse(400, { error: { code: 'VALIDATION_ERROR', message: 'Bad request.' } }),
    )

    await apiFetch('/columns').catch(() => undefined)

    expect(onUnauthorized).not.toHaveBeenCalled()
  })
})
