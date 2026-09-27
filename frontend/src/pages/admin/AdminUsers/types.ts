export type User = {
    id: string
    username: string
    email: string | null
    employeeCode: string | null
    firstName: string | null
    lastName: string | null
    companyCode: string | null
    // ISO date (yyyy-MM-dd) without a time zone.
    employmentDate: string | null
    electability: string | null
    createdAt: string
}

// A row in the users table; the full User is fetched when opening details or edit.
export type UserListItem = {
    id: string
    name: string | null
    username: string
}

export type PagedUsers = {
    items: UserListItem[]
    totalCount: number
    page: number
    pageSize: number
}

export type ImportMode = 'emails' | 'employees'

export type ImportSummary = {
    created: number
    skipped: number
    warnings: { row: number; message: string }[]
}
