import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { useSessionKeepAlive } from '../hooks/useSessionKeepAlive'

type LoginResult = { success: boolean; error?: string }

function toFullName(body: { firstName?: string | null; lastName?: string | null }) {
    return [body.firstName, body.lastName].filter(Boolean).join(' ') || null
}

type UserAuthContextType = {
    isAuthenticated: boolean
    isLoading: boolean
    // True when the session ended because of inactivity (the login page says so).
    sessionExpired: boolean
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

    const checkSession = async () => {
        try {
            const res = await fetch('http://localhost:5120/api/auth/user/me', {
                credentials: 'include',
            })
            if (res.ok) {
                const body = await res.json()
                setUsername(body.username)
                setFullName(toFullName(body))
                setIsAuthenticated(true)
            } else {
                setIsAuthenticated(false)
            }
        } catch {
            setIsAuthenticated(false)
        } finally {
            setIsLoading(false)
        }
    }

    useEffect(() => {
        void checkSession()
    }, [])

    const handleExpired = useCallback(() => {
        setIsAuthenticated(false)
        setSessionExpired(true)
    }, [])

    useSessionKeepAlive(isAuthenticated, 'http://localhost:5120/api/auth/user/refresh', handleExpired)

    const login = async (loginUsername: string, password: string): Promise<LoginResult> => {
        const res = await fetch('http://localhost:5120/api/auth/user/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ username: loginUsername, password }),
        })

        if (!res.ok) {
            return { success: false, error: 'Invalid username or password.' }
        }

        setSessionExpired(false)
        // Use the stored username rather than what was typed, since login is case-insensitive.
        const body = await res.json()
        setUsername(body.username)
        setFullName(toFullName(body))
        setIsAuthenticated(true)
        return { success: true }
    }

    const logout = async () => {
        setIsAuthenticated(false)
        setUsername(null)
        setFullName(null)
        await fetch('http://localhost:5120/api/auth/user/logout', {
            method: 'POST',
            credentials: 'include',
        })
    }

    return (
        <UserAuthContext.Provider value={{ isAuthenticated, isLoading, sessionExpired, username, fullName, login, logout }}>
            {children}
        </UserAuthContext.Provider>
    )
}

export function useUserAuth() {
    const ctx = useContext(UserAuthContext)
    if (!ctx) throw new Error('useUserAuth must be used within UserAuthProvider')
    return ctx
}