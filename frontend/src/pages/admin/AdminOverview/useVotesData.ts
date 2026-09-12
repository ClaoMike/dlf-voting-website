import { useEffect, useState } from 'react'
import type { PagedVotes, Tab, VoteRow, VotingOption, VoteStats } from './types'

const STATS_API = 'http://localhost:5120/api/votes/stats'
const VOTES_API = 'http://localhost:5120/api/votes'
const OPTIONS_API = 'http://localhost:5120/api/voting-options'

export function useVotesData() {
    const [tab, setTab] = useState<Tab>('all')
    const [votes, setVotes] = useState<VoteRow[]>([])
    const [options, setOptions] = useState<VotingOption[]>([])
    const [page, setPage] = useState(1)
    const [totalCount, setTotalCount] = useState(0)
    const [pageSize, setPageSize] = useState(25)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [stats, setStats] = useState<VoteStats | null>(null)

    const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1)

    const fetchVotes = async (targetPage: number, targetTab: Tab) => {
        setIsLoading(true)
        setError(null)
        try {
            const onlyVoted = targetTab === 'voted'
            const res = await fetch(`${VOTES_API}?page=${targetPage}&onlyVoted=${onlyVoted}`, {
                credentials: 'include',
            })
            if (!res.ok) throw new Error('Failed to load votes.')
            const data: PagedVotes = await res.json()
            setVotes(data.items)
            setTotalCount(data.totalCount)
            setPageSize(data.pageSize)
            setPage(data.page)
        } catch {
            setError('Could not load votes.')
        } finally {
            setIsLoading(false)
        }
    }

    const fetchOptions = async () => {
        try {
            const res = await fetch(OPTIONS_API, { credentials: 'include' })
            if (res.ok) setOptions(await res.json())
        } catch {
            // options are only needed for the edit dialog
        }
    }

    const fetchStats = async () => {
        try {
            const res = await fetch(STATS_API, { credentials: 'include' })
            if (res.ok) setStats(await res.json())
        } catch {
            // stats are supplementary; a failure here doesn't block the table
        }
    }

    useEffect(() => {
        fetchVotes(1, tab)
        fetchOptions()
        fetchStats()
    }, [tab])

    const changeTab = (newTab: Tab) => {
        if (newTab === tab) return
        setTab(newTab)
    }

    const refresh = async () => {
        await fetchVotes(page, tab)
        await fetchStats()
    }

    return {
        tab,
        votes,
        options,
        page,
        totalPages,
        isLoading,
        error,
        stats,
        changeTab,
        goToPage: (newPage: number) => fetchVotes(newPage, tab),
        refresh,
    }
}