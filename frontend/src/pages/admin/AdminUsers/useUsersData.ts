import { useEffect, useState } from 'react'
import type { PagedUsers, User } from './types'

const API_BASE = 'http://localhost:5120/api/users'

export function useUsersData() {
    const [users, setUsers] = useState<User[]>([])
    const [page, setPage] = useState(1)
    const [totalCount, setTotalCount] = useState(0)
    const [pageSize, setPageSize] = useState(25)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1)

    const fetchUsers = async (targetPage: number) => {
        setIsLoading(true)
        setError(null)
        try {
            const res = await fetch(`${API_BASE}?page=${targetPage}`, { credentials: 'include' })
            if (!res.ok) throw new Error('Failed to load users.')
            const data: PagedUsers = await res.json()
            setUsers(data.items)
            setTotalCount(data.totalCount)
            setPageSize(data.pageSize)
            setPage(data.page)
        } catch {
            setError('Could not load users.')
        } finally {
            setIsLoading(false)
        }
    }

    useEffect(() => {
        fetchUsers(1)
    }, [])

    return { users, page, totalPages, isLoading, error, fetchUsers }
}