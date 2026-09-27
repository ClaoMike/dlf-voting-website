export type Administrator = {
    id: string
    username: string
    createdAt: string
}

export type PagedAdministrators = {
    items: Administrator[]
    totalCount: number
    page: number
    pageSize: number
}