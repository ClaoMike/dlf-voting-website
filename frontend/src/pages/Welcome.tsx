import { useEffect, useState } from 'react'
import { useUserAuth } from '../context/UserAuthContext'
import './Welcome.css'

type VotingOption = {
    id: string
    name: string
    createdAt: string
}

const API_BASE = 'http://localhost:5120/api/voting-options'

function Welcome() {
    const { email } = useUserAuth()
    const [options, setOptions] = useState<VotingOption[]>([])
    const [selectedId, setSelectedId] = useState('')
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        const fetchOptions = async () => {
            setIsLoading(true)
            setError(null)
            try {
                const res = await fetch(API_BASE, { credentials: 'include' })
                if (!res.ok) throw new Error('Failed to load voting options.')
                const data = await res.json()
                setOptions(data)
            } catch {
                setError('Could not load voting options.')
            } finally {
                setIsLoading(false)
            }
        }

        fetchOptions()
    }, [])

    const handleSubmitVote = () => {
        // TODO: call the vote submission endpoint once it exists.
    }

    return (
        <div className="welcome-page">
            <h1>Welcome, {email}</h1>

            <section className="vote-section">
                {isLoading ? (
                    <p>Loading voting options...</p>
                ) : error ? (
                    <p className="voting-options-error">{error}</p>
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
        </div>
    )
}

export default Welcome