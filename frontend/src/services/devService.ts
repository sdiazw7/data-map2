import type { JoinResponse, WorkspaceSummary } from '../types/api'
import { apiFetch } from '../utils/api'

export async function getDevWorkspaces(): Promise<WorkspaceSummary[]> {
  return apiFetch<WorkspaceSummary[]>('/dev/workspaces')
}

export async function joinDevWorkspace(id: string): Promise<JoinResponse> {
  return apiFetch<JoinResponse>(`/dev/workspaces/${id}/join`, {
    method: 'POST',
  })
}
