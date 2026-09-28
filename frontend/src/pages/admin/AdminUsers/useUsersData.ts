import { useEffect, useState } from 'react'
import type { PagedUsers, UserListItem } from './types'

const API_BASE = '/api/users'
const LOAD_ERROR = 'Could not load users.'

type LoadResult = { data: PagedUsers } | { error: string }

// Only talks to the server; the hook puts the result into state.
async function requestUsers(targetPage: number): Promise<LoadResult> {
    try {
        const res = await fetch(`${API_BASE}?page=${targetPage}`, { credentials: 'include' })
        if (!res.ok) return { error: LOAD_ERROR }
        return { data: await res.json() }
    } catch {
        return { error: LOAD_ERROR }
    }
}

export function useUsersData() {
    const [users, setUsers] = useState<UserListItem[]>([])
    const [page, setPage] = useState(1)
    const [totalCount, setTotalCount] = useState(0)
    const [pageSize, setPageSize] = useState(25)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1)

    const showResult = (result: LoadResult) => {
        if ('error' in result) {
            setError(result.error)
        } else {
            setUsers(result.data.items)
            setTotalCount(result.data.totalCount)
            setPageSize(result.data.pageSize)
            setPage(result.data.page)
        }
        setIsLoading(false)
    }

    const fetchUsers = async (targetPage: number) => {
        setIsLoading(true)
        setError(null)
        showResult(await requestUsers(targetPage))
    }

    // First page on mount. An answer that arrives after the page is left is ignored.
    useEffect(() => {
        let ignore = false
        void requestUsers(1).then((result) => {
            if (!ignore) showResult(result)
        })
        return () => {
            ignore = true
        }
    }, [])

    return { users, page, totalPages, isLoading, error, fetchUsers }
}
