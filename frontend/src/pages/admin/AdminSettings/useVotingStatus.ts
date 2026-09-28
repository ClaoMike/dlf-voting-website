import { useEffect, useState } from 'react'

const STATUS_API = '/api/settings/voting'

export function useVotingStatus() {
    const [isVotingOpen, setIsVotingOpen] = useState<boolean | null>(null)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    // Load on mount. An answer that arrives after the page is left is ignored.
    useEffect(() => {
        let ignore = false
        const load = async () => {
            const res = await fetch(STATUS_API, { credentials: 'include' })
            if (!res.ok) throw new Error('Failed to load settings.')
            const data: { isVotingOpen: boolean } = await res.json()
            return data.isVotingOpen
        }
        load()
            .then((open) => {
                if (!ignore) setIsVotingOpen(open)
            })
            .catch(() => {
                if (!ignore) setError('Could not load settings.')
            })
            .finally(() => {
                if (!ignore) setIsLoading(false)
            })
        return () => {
            ignore = true
        }
    }, [])

    const toggle = async () => {
        if (isVotingOpen === null) return
        setError(null)
        const newValue = !isVotingOpen
        try {
            const res = await fetch(STATUS_API, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ isVotingOpen: newValue }),
            })
            if (!res.ok) {
                setError('Failed to update setting.')
                return
            }
            const data = await res.json()
            setIsVotingOpen(data.isVotingOpen)
        } catch {
            setError('Could not update setting.')
        }
    }

    return { isVotingOpen, isLoading, error, toggle }
}