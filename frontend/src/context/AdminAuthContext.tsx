import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { useSessionTimeout } from '../hooks/useSessionTimeout'
import { AdminAuthContext, type LoginResult } from './useAdminAuth'

export function AdminAuthProvider({ children }: { children: ReactNode }) {
    const [isAuthenticated, setIsAuthenticated] = useState(false)
    const [isLoading, setIsLoading] = useState(true)
    const [sessionExpired, setSessionExpired] = useState(false)
    const [username, setUsername] = useState<string | null>(null)

    const handleExpired = useCallback(() => {
        setIsAuthenticated(false)
        setSessionExpired(true)
    }, [])

    const session = useSessionTimeout({
        isSignedIn: isAuthenticated,
        refreshUrl: '/api/auth/admin/refresh',
        logoutUrl: '/api/auth/admin/logout',
        onExpired: handleExpired,
    })

    // Once, on page load. Only uses state setters and markRenewed, which never change.
    useEffect(() => {
        const checkSession = async () => {
            const res = await fetch('/api/auth/admin/me', {
                credentials: 'include',
            })
            return res.ok ? await res.json() : null
        }
        checkSession()
            .then((body) => {
                if (!body) {
                    setIsAuthenticated(false)
                    return
                }
                setUsername(body.username)
                setIsAuthenticated(true)
                session.markRenewed()
            })
            .catch(() => setIsAuthenticated(false))
            .finally(() => setIsLoading(false))
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])

    const login = async (loginUsername: string, password: string): Promise<LoginResult> => {
        const res = await fetch('/api/auth/admin/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ username: loginUsername, password }),
        })

        if (res.status === 429) {
            return { success: false, error: 'Too many sign-in attempts. Please wait a few minutes and try again.' }
        }
        if (!res.ok) {
            return { success: false, error: 'Invalid username or password.' }
        }

        setSessionExpired(false)
        // Use the stored username rather than what was typed, since login is case-insensitive.
        const body = await res.json()
        setUsername(body.username)
        setIsAuthenticated(true)
        session.markRenewed()
        return { success: true }
    }

    const logout = async () => {
        setIsAuthenticated(false)
        setUsername(null)
        await fetch('/api/auth/admin/logout', {
            method: 'POST',
            credentials: 'include',
        })
    }

    return (
        <AdminAuthContext.Provider value={{ isAuthenticated, isLoading, sessionExpired, sessionWarningSecondsLeft: session.warningSecondsLeft, staySignedIn: session.staySignedIn, username, login, logout }}>
            {children}
        </AdminAuthContext.Provider>
    )
}
