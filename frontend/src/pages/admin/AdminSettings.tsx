import { useEffect, useState } from 'react'
import './AdminSettings.css'

const STATUS_API = 'http://localhost:5120/api/settings/voting'

function AdminSettings() {
    const [isVotingOpen, setIsVotingOpen] = useState<boolean | null>(null)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const fetchStatus = async () => {
        setIsLoading(true)
        setError(null)
        try {
            const res = await fetch(STATUS_API, { credentials: 'include' })
            if (!res.ok) throw new Error('Failed to load settings.')
            const data = await res.json()
            setIsVotingOpen(data.isVotingOpen)
        } catch {
            setError('Could not load settings.')
        } finally {
            setIsLoading(false)
        }
    }

    useEffect(() => {
        fetchStatus()
    }, [])

    const handleToggle = async () => {
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
            if (!res.ok) throw new Error('Failed to update setting.')
            const data = await res.json()
            setIsVotingOpen(data.isVotingOpen)
        } catch {
            setError('Could not update setting.')
        }
    }

    return (
        <div className="voting-options-page">
            <h1>Settings</h1>

            {error && <p className="voting-options-error">{error}</p>}

            {isLoading ? (
                <p>Loading...</p>
            ) : (
                <div className="settings-toggle-row">
          <span className="settings-toggle-label">
            Voting is currently {isVotingOpen ? 'open' : 'closed'}
          </span>
                    <button
                        className={isVotingOpen ? 'settings-toggle-button settings-toggle-close' : 'settings-toggle-button settings-toggle-open'}
                        onClick={handleToggle}
                    >
                        {isVotingOpen ? 'Close voting' : 'Open voting'}
                    </button>
                </div>
            )}
        </div>
    )
}

export default AdminSettings