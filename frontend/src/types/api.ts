// API request and response types — populated per feature

/** The envelope every list endpoint returns. */
export type PagedResult<T> = {
  items: T[]
  /** Rows matching the query across all pages, ignoring limit and offset. */
  total: number
  limit: number
  offset: number
}

/** The single error shape the API returns for every failure. */
export type ApiErrorResponse = {
  error: {
    code: string
    message: string
  }
}

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

/** Body of PUT /columns/{columnId}/business-term; the column comes from the route. */
export type BusinessTermMappingRequest = {
  termId: string
}

/** A column's version after an edit — the token for the next optimistic write. */
export type ColumnVersion = {
  columnId: string
  version: number
}

/** A row the server declined as stale, with the version it holds now. */
export type ColumnConflict = {
  columnId: string
  currentVersion: number
}

/**
 * The applied rows and the declined ones. A stale row does not fail the request, so a pasted
 * range survives one cell moving under the user.
 */
export type BulkUpdateResponse = {
  columns: ColumnVersion[]
  conflicts: ColumnConflict[]
}

export type ImportSummary = {
  rows: number
  schemas: number
  tables: number
  columnsCreated: number
  columnsUpdated: number
}
