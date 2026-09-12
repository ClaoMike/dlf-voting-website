import { useEffect, useState } from 'react'
import type { PagedAdministrators, Administrator } from './types'

const API_BASE = 'http://localhost:5120/api/administrators'

export function useAdministratorsData() {
    const [admins, setAdmins] = useState<Administrator[]>([])
    const [page, setPage] = useState(1)
    const [totalCount, setTotalCount] = useState(0)
    const [pageSize, setPageSize] = useState(25)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1)

    const fetchAdmins = async (targetPage: number) => {
        setIsLoading(true)
        setError(null)
        try {
            const res = await fetch(`${API_BASE}?page=${targetPage}`, { credentials: 'include' })
            if (!res.ok) throw new Error('Failed to load administrators.')
            const data: PagedAdministrators = await res.json()
            setAdmins(data.items)
            setTotalCount(data.totalCount)
            setPageSize(data.pageSize)
            setPage(data.page)
        } catch {
            setError('Could not load administrators.')
        } finally {
            setIsLoading(false)
        }
    }

    useEffect(() => {
        fetchAdmins(1)
    }, [])

    return { admins, page, totalPages, isLoading, error, fetchAdmins }
}