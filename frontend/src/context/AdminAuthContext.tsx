import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'

type LoginResult = { success: boolean; error?: string }

type AdminAuthContextType = {
    isAuthenticated: boolean
    isLoading: boolean
    username: string | null
    login: (username: string, password: string) => Promise<LoginResult>
    logout: () => Promise<void>
}

const AdminAuthContext = createContext<AdminAuthContextType | undefined>(undefined)

export function AdminAuthProvider({ children }: { children: ReactNode }) {
    const [isAuthenticated, setIsAuthenticated] = useState(false)
    const [isLoading, setIsLoading] = useState(true)
    const [username, setUsername] = useState<string | null>(null)

    const checkSession = async () => {
        try {
            const res = await fetch('http://localhost:5120/api/auth/admin/me', {
                credentials: 'include',
            })
            if (res.ok) {
                const body = await res.json()
                setUsername(body.username)
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

    const login = async (loginUsername: string, password: string): Promise<LoginResult> => {
        const res = await fetch('http://localhost:5120/api/auth/admin/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ username: loginUsername, password }),
        })

        if (!res.ok) {
            return { success: false, error: 'Invalid username or password.' }
        }

        // Use the stored username rather than what was typed, since login is case-insensitive.
        const body = await res.json()
        setUsername(body.username)
        setIsAuthenticated(true)
        return { success: true }
    }

    const logout = async () => {
        setIsAuthenticated(false)
        setUsername(null)
        await fetch('http://localhost:5120/api/auth/admin/logout', {
            method: 'POST',
            credentials: 'include',
        })
    }

    return (
        <AdminAuthContext.Provider value={{ isAuthenticated, isLoading, username, login, logout }}>
            {children}
        </AdminAuthContext.Provider>
    )
}

export function useAdminAuth() {
    const ctx = useContext(AdminAuthContext)
    if (!ctx) throw new Error('useAdminAuth must be used within AdminAuthProvider')
    return ctx
}