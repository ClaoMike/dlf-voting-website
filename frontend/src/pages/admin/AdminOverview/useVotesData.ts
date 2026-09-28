import { useEffect, useState } from 'react'
import type { PagedVotes, Tab, VoteRow, VoteStats } from './types'

const STATS_API = '/api/votes/stats'
const VOTES_API = '/api/votes'
const VOTES_ERROR = 'Could not load votes.'

type VotesResult = { data: PagedVotes } | { error: string }

// These only talk to the server; the hook puts the results into state.
async function requestVotes(targetPage: number, targetTab: Tab): Promise<VotesResult> {
    try {
        const onlyVoted = targetTab === 'voted'
        const res = await fetch(`${VOTES_API}?page=${targetPage}&onlyVoted=${onlyVoted}`, {
            credentials: 'include',
        })
        if (!res.ok) return { error: VOTES_ERROR }
        return { data: await res.json() }
    } catch {
        return { error: VOTES_ERROR }
    }
}

// Stats are supplementary: a failure leaves them out (null).
async function requestStats(): Promise<VoteStats | null> {
    try {
        const res = await fetch(STATS_API, { credentials: 'include' })
        return res.ok ? await res.json() : null
    } catch {
        return null
    }
}

export function useVotesData() {
    const [tab, setTab] = useState<Tab>('all')
    const [votes, setVotes] = useState<VoteRow[]>([])
    const [page, setPage] = useState(1)
    const [totalCount, setTotalCount] = useState(0)
    const [pageSize, setPageSize] = useState(25)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [stats, setStats] = useState<VoteStats | null>(null)

    const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1)

    const showVotes = (result: VotesResult) => {
        if ('error' in result) {
            setError(result.error)
        } else {
            setVotes(result.data.items)
            setTotalCount(result.data.totalCount)
            setPageSize(result.data.pageSize)
            setPage(result.data.page)
        }
        setIsLoading(false)
    }

    const showStats = (result: VoteStats | null) => {
        if (result) setStats(result)
    }

    const fetchVotes = async (targetPage: number, targetTab: Tab) => {
        setIsLoading(true)
        setError(null)
        showVotes(await requestVotes(targetPage, targetTab))
    }

    // First page of the tab, on mount and when the tab changes. Answers for a tab that is no longer shown are
    // ignored, so a slow one can't overwrite the current tab.
    useEffect(() => {
        let ignore = false
        void requestVotes(1, tab).then((result) => {
            if (!ignore) showVotes(result)
        })
        void requestStats().then((result) => {
            if (!ignore) showStats(result)
        })
        return () => {
            ignore = true
        }
    }, [tab])

    const changeTab = (newTab: Tab) => {
        if (newTab === tab) return
        setIsLoading(true)
        setError(null)
        setTab(newTab)
    }

    const refresh = async () => {
        await fetchVotes(page, tab)
        showStats(await requestStats())
    }

    return {
        tab,
        votes,
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