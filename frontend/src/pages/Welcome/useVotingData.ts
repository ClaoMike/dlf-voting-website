import { useEffect, useState } from 'react'
import type { MyVote, VotingOption } from './types'

const OPTIONS_API = 'http://localhost:5120/api/voting-options'
const VOTES_API = 'http://localhost:5120/api/votes'
const STATUS_API = 'http://localhost:5120/api/settings/voting'

export function useVotingData() {
    const [options, setOptions] = useState<VotingOption[]>([])
    const [myVote, setMyVote] = useState<MyVote | null>(null)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [isVotingOpen, setIsVotingOpen] = useState<boolean | null>(null)

    useEffect(() => {
        const fetchAll = async () => {
            setIsLoading(true)
            setError(null)
            try {
                const [statusRes, optionsRes, voteRes] = await Promise.all([
                    fetch(STATUS_API, { credentials: 'include' }),
                    fetch(OPTIONS_API, { credentials: 'include' }),
                    fetch(`${VOTES_API}/me`, { credentials: 'include' }),
                ])

                if (statusRes.ok) {
                    const statusData = await statusRes.json()
                    setIsVotingOpen(statusData.isVotingOpen)
                }

                // Only bother loading options/vote data if voting is actually open —
                // if closed, those endpoints would 403 anyway.
                if (optionsRes.ok) setOptions(await optionsRes.json())
                if (voteRes.ok) setMyVote(await voteRes.json())
            } catch {
                setError('Could not load voting data.')
            } finally {
                setIsLoading(false)
            }
        }

        fetchAll()
    }, [])

    const submitVote = async (optionId: string) => {
        setError(null)
        try {
            const res = await fetch(VOTES_API, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ votingOptionId: optionId }),
            })

            if (res.status === 403) {
                setIsVotingOpen(false)
                return false
            }

            if (!res.ok) throw new Error('Failed to submit vote.')

            setMyVote(await res.json())
            return true
        } catch {
            setError('Could not submit your vote.')
            return false
        }
    }

    return { options, myVote, isLoading, error, isVotingOpen, submitVote }
}