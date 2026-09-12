import { useState } from 'react'
import { isValidPassword } from '../../../utils/validation'

const CHANGE_PASSWORD_API = 'http://localhost:5120/api/administrators/me/password'

export function useChangePassword() {
    const [newPassword, setNewPassword] = useState('')
    const [error, setError] = useState<string | null>(null)
    const [revealPassword, setRevealPassword] = useState<string | null>(null)

    const isValid = isValidPassword(newPassword)

    const submit = async () => {
        setError(null)
        try {
            const res = await fetch(CHANGE_PASSWORD_API, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ password: newPassword }),
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to change password.')
                return
            }

            setRevealPassword(newPassword)
            setNewPassword('')
        } catch {
            setError('Failed to change password.')
        }
    }

    return {
        newPassword,
        setNewPassword,
        isValid,
        error,
        submit,
        revealPassword,
        closeReveal: () => setRevealPassword(null),
    }
}