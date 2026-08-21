import type { JoinResponse, PagedResult, WorkspaceSummary } from '../types/api'
import { apiFetch } from '../utils/api'

export async function getDevWorkspaces(limit = 200, offset = 0): Promise<PagedResult<WorkspaceSummary>> {
  return apiFetch<PagedResult<WorkspaceSummary>>(`/dev/workspaces?limit=${limit}&offset=${offset}`)
}

export async function joinDevWorkspace(id: string): Promise<JoinResponse> {
  return apiFetch<JoinResponse>(`/dev/workspaces/${id}/join`, {
    method: 'POST',
  })
}
