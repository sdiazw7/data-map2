import type { InviteDto, JoinRequest, JoinResponse } from '../types/api'
import { apiFetch } from '../utils/api'

export async function getInvite(token: string): Promise<InviteDto> {
  return apiFetch<InviteDto>(`/invite/${token}`)
}

export async function joinInvite(token: string, req: JoinRequest): Promise<JoinResponse> {
  return apiFetch<JoinResponse>(`/invite/${token}/join`, {
    method: 'POST',
    body: JSON.stringify(req),
  })
}
