import { useEffect, useState } from 'react'
import { useUserAuth } from '../context/UserAuthContext'
import EditVoteDialog from '../components/EditVoteDialog'
import './Welcome.css'

type VotingOption = {
    id: string
    name: string
    createdAt: string
}

type MyVote = {
    hasVoted: boolean
    votingOptionId: string | null
    votingOptionName: string | null
    updatedAt: string | null
}

const OPTIONS_API = 'http://localhost:5120/api/voting-options'
const VOTES_API = 'http://localhost:5120/api/votes'
const STATUS_API = 'http://localhost:5120/api/settings/voting'

function Welcome() {
    const { email } = useUserAuth()
    const [options, setOptions] = useState<VotingOption[]>([])
    const [myVote, setMyVote] = useState<MyVote | null>(null)
    const [selectedId, setSelectedId] = useState('')
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [showEditVote, setShowEditVote] = useState(false)
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

    const handleSubmitVote = async () => {
        if (!selectedId) return
        setError(null)
        try {
            const res = await fetch(VOTES_API, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ votingOptionId: selectedId }),
            })

            if (res.status === 403) {
                setIsVotingOpen(false)
                return
            }

            if (!res.ok) throw new Error('Failed to submit vote.')

            setMyVote(await res.json())
            setSelectedId('')
        } catch {
            setError('Could not submit your vote.')
        }
    }

    const handleEditVoteSave = async (optionId: string) => {
        setError(null)
        try {
            const res = await fetch(VOTES_API, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ votingOptionId: optionId }),
            })

            if (!res.ok) throw new Error('Failed to update vote.')

            setMyVote(await res.json())
            setShowEditVote(false)
        } catch {
            setError('Could not update your vote.')
            setShowEditVote(false)
        }
    }

    return (
        <div className="welcome-page">
            <h1>
                Welcome, <span className="welcome-email">{email}</span>
            </h1>

            <section className="vote-section">
                {isLoading ? (
                    <p>Loading voting options...</p>
                ) : isVotingOpen === false ? (
                    <p className="voting-closed-message">Voting polls are closed.</p>
                ) : error ? (
                    <p className="voting-options-error">{error}</p>
                ) : myVote?.hasVoted ? (
                    <>
                        <p className="current-vote-text">
                            You voted for: <strong>{myVote.votingOptionName}</strong>
                        </p>
                        <button className="vote-submit" onClick={() => setShowEditVote(true)}>
                            Edit Vote
                        </button>
                    </>
                ) : (
                    <>
                        <select
                            className="vote-select"
                            value={selectedId}
                            onChange={(e) => setSelectedId(e.target.value)}
                        >
                            <option value=""></option>
                            {options.map((option) => (
                                <option key={option.id} value={option.id}>
                                    {option.name}
                                </option>
                            ))}
                        </select>

                        <button
                            className="vote-submit"
                            disabled={selectedId === ''}
                            onClick={handleSubmitVote}
                        >
                            Submit vote
                        </button>
                    </>
                )}
            </section>

            {showEditVote && myVote?.votingOptionId && (
                <EditVoteDialog
                    options={options}
                    initialOptionId={myVote.votingOptionId}
                    onSave={handleEditVoteSave}
                    onCancel={() => setShowEditVote(false)}
                />
            )}
        </div>
    )
}

export default Welcome