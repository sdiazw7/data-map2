// API request and response types — populated per feature

export type InviteDto = {
  id: string
  workspaceId: string
  workspaceName: string
  expiresAt: string
  isValid: boolean
}

export type JoinRequest = {
  email: string
}

export type JoinResponse = {
  participantId: string
  workspaceId: string
  workspaceName: string
  email: string
}

export type WorkspaceSummary = {
  id: string
  name: string
}

export type ColumnGridRow = {
  columnId: string
  schemaName: string
  tableName: string
  columnName: string
  dataType: string
  exampleValue: string | null
  description: string | null
  businessTerm: string | null
  owner: string | null
  version: number
}

export type ColumnUpdateRequest = {
  columnId: string
  description: string | null
  exampleValue: string | null
  owner: string | null
  version: number
}

export type CoverageResponse = {
  totalColumns: number
  documentedColumns: number
  coveragePercent: number
}

export type BusinessTermDto = {
  id: string
  name: string
  definition: string
}

export type BusinessTermCreateRequest = {
  name: string
  definition: string
}

export type TermMappingRequest = {
  termId: string
  columnId: string
}
