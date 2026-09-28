import { useEffect, useState } from 'react'
import type { VotingOption } from './types'

const API_BASE = '/api/voting-options'
const LOAD_ERROR = 'Could not load voting options.'

type LoadResult = { data: VotingOption[] } | { error: string }

// Only talks to the server; the hook puts the result into state.
async function requestOptions(): Promise<LoadResult> {
    try {
        const res = await fetch(API_BASE, { credentials: 'include' })
        if (!res.ok) return { error: LOAD_ERROR }
        return { data: await res.json() }
    } catch {
        return { error: LOAD_ERROR }
    }
}

export function useVotingOptionsData() {
    const [options, setOptions] = useState<VotingOption[]>([])
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const showResult = (result: LoadResult) => {
        if ('error' in result) setError(result.error)
        else setOptions(result.data)
        setIsLoading(false)
    }

    const fetchOptions = async () => {
        setIsLoading(true)
        setError(null)
        showResult(await requestOptions())
    }

    // Load on mount. An answer that arrives after the page is left is ignored.
    useEffect(() => {
        let ignore = false
        void requestOptions().then((result) => {
            if (!ignore) showResult(result)
        })
        return () => {
            ignore = true
        }
    }, [])

    return { options, isLoading, error, fetchOptions }
}
