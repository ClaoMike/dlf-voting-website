import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { useSessionTimeout } from '../hooks/useSessionTimeout'

type LoginResult = { success: boolean; error?: string }

function toFullName(body: { firstName?: string | null; lastName?: string | null }) {
    return [body.firstName, body.lastName].filter(Boolean).join(' ') || null
}

type UserAuthContextType = {
    isAuthenticated: boolean
    isLoading: boolean
    // True when the session ended because of inactivity (the login page says so).
    sessionExpired: boolean
    // Seconds until the session ends, while the "Are you still there?" warning should show; null otherwise.
    sessionWarningSecondsLeft: number | null
    staySignedIn: () => Promise<void>
    username: string | null
    // "First Last" from the employee import; null when the user has no name on record.
    fullName: string | null
    login: (username: string, password: string) => Promise<LoginResult>
    logout: () => Promise<void>
}

const UserAuthContext = createContext<UserAuthContextType | undefined>(undefined)

export function UserAuthProvider({ children }: { children: ReactNode }) {
    const [isAuthenticated, setIsAuthenticated] = useState(false)
    const [isLoading, setIsLoading] = useState(true)
    const [sessionExpired, setSessionExpired] = useState(false)
    const [username, setUsername] = useState<string | null>(null)
    const [fullName, setFullName] = useState<string | null>(null)

    const handleExpired = useCallback(() => {
        setIsAuthenticated(false)
        setSessionExpired(true)
    }, [])

    const session = useSessionTimeout({
        isSignedIn: isAuthenticated,
        refreshUrl: '/api/auth/user/refresh',
        logoutUrl: '/api/auth/user/logout',
        onExpired: handleExpired,
    })

    const checkSession = async () => {
        try {
            const res = await fetch('/api/auth/user/me', {
                credentials: 'include',
            })
            if (res.ok) {
                const body = await res.json()
                setUsername(body.username)
                setFullName(toFullName(body))
                setIsAuthenticated(true)
                session.markRenewed()
            } else {
                setIsAuthenticated(false)
            }
        } catch {
            setIsAuthenticated(false)
        } finally {
            setIsLoading(false)
        }
    }

    // Once, on page load. checkSession only uses state setters and markRenewed, which never change.
    useEffect(() => {
        void checkSession()
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])

    const login = async (loginUsername: string, password: string): Promise<LoginResult> => {
        const res = await fetch('/api/auth/user/login', {
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
        setFullName(toFullName(body))
        setIsAuthenticated(true)
        session.markRenewed()
        return { success: true }
    }

    const logout = async () => {
        setIsAuthenticated(false)
        setUsername(null)
        setFullName(null)
        await fetch('/api/auth/user/logout', {
            method: 'POST',
            credentials: 'include',
        })
    }

    return (
        <UserAuthContext.Provider value={{ isAuthenticated, isLoading, sessionExpired, sessionWarningSecondsLeft: session.warningSecondsLeft, staySignedIn: session.staySignedIn, username, fullName, login, logout }}>
            {children}
        </UserAuthContext.Provider>
    )
}

export function useUserAuth() {
    const ctx = useContext(UserAuthContext)
    if (!ctx) throw new Error('useUserAuth must be used within UserAuthProvider')
    return ctx
}