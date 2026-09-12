export type User = {
    id: string
    email: string
    createdAt: string
}

export type PagedUsers = {
    items: User[]
    totalCount: number
    page: number
    pageSize: number
}