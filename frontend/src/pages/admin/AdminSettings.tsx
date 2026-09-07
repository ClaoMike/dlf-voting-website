import { useEffect, useState } from 'react'
import PasswordField from '../../components/PasswordField'
import { isValidPassword } from '../../utils/validation'
import './AdminSettings.css'
import PasswordRevealDialog from '../../components/PasswordRevealDialog'
import { useAdminAuth } from '../../context/AdminAuthContext'

const STATUS_API = 'http://localhost:5120/api/settings/voting'
const CHANGE_PASSWORD_API = 'http://localhost:5120/api/administrators/me/password'

function AdminSettings() {
    const [isVotingOpen, setIsVotingOpen] = useState<boolean | null>(null)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const [newPassword, setNewPassword] = useState('')
    const [passwordError, setPasswordError] = useState<string | null>(null)
    const [passwordSuccess, setPasswordSuccess] = useState(false)

    const { email } = useAdminAuth()
    const [revealPassword, setRevealPassword] = useState<string | null>(null)

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

    const passwordValid = isValidPassword(newPassword)

    const handleChangePassword = async () => {
        setPasswordError(null)
        try {
            const res = await fetch(CHANGE_PASSWORD_API, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ password: newPassword }),
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setPasswordError(body?.message ?? 'Failed to change password.')
                return
            }

            setRevealPassword(newPassword)
            setNewPassword('')
        } catch {
            setPasswordError('Failed to change password.')
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

            <section className="settings-password-section">
                <h2>Change your password</h2>

                <PasswordField
                    value={newPassword}
                    onChange={(value) => {
                        setNewPassword(value)
                        setPasswordSuccess(false)
                    }}
                    placeholder="New password"
                />
                {newPassword.length > 0 && !passwordValid && (
                    <p className="voting-options-error">
                        Password must be 20-64 characters with at least one uppercase letter, one digit,
                        and one special character.
                    </p>
                )}

                {passwordError && <p className="voting-options-error">{passwordError}</p>}
                {passwordSuccess && <p className="settings-password-success">Password updated successfully.</p>}

                <button
                    className="settings-toggle-button settings-toggle-open"
                    disabled={!passwordValid}
                    onClick={handleChangePassword}
                >
                    Update password
                </button>
            </section>

            {revealPassword && email && (
                <PasswordRevealDialog
                    email={email}
                    password={revealPassword}
                    onClose={() => setRevealPassword(null)}
                />
            )}
            
        </div>
    )
}

export default AdminSettings