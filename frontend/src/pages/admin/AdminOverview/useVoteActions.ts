import { useState } from 'react'
import type { VoteRow } from './types'

const VOTES_API = '/api/votes'

export function useVoteActions(onChanged: () => Promise<void>) {
    const [resettingVote, setResettingVote] = useState<VoteRow | null>(null)
    const [error, setError] = useState<string | null>(null)

    const confirmReset = async () => {
        if (!resettingVote) return
        setError(null)
        try {
            const res = await fetch(`${VOTES_API}/${resettingVote.userId}`, {
                method: 'DELETE',
                credentials: 'include',
            })
            if (!res.ok && res.status !== 404) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to reset vote.')
            }
            setResettingVote(null)
            await onChanged()
        } catch {
            setError('Failed to reset vote.')
            setResettingVote(null)
        }
    }

    return {
        resettingVote,
        error,
        startReset: setResettingVote,
        cancelReset: () => setResettingVote(null),
        confirmReset,
    }
}
