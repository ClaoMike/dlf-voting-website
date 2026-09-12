export type Administrator = {
    id: string
    email: string
    createdAt: string
}

export type PagedAdministrators = {
    items: Administrator[]
    totalCount: number
    page: number
    pageSize: number
}