import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'

type LoginResult = { success: boolean; error?: string }

type AdminAuthContextType = {
    isAuthenticated: boolean
    isLoading: boolean
    email: string | null
    login: (email: string, password: string) => Promise<LoginResult>
    logout: () => Promise<void>
}

const AdminAuthContext = createContext<AdminAuthContextType | undefined>(undefined)

export function AdminAuthProvider({ children }: { children: ReactNode }) {
    const [isAuthenticated, setIsAuthenticated] = useState(false)
    const [isLoading, setIsLoading] = useState(true)
    const [email, setEmail] = useState<string | null>(null)

    const checkSession = async () => {
        try {
            const res = await fetch('http://localhost:5120/api/auth/admin/me', {
                credentials: 'include',
            })
            if (res.ok) {
                const body = await res.json()
                setEmail(body.email)
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
        checkSession()
    }, [])

    const login = async (loginEmail: string, password: string): Promise<LoginResult> => {
        const res = await fetch('http://localhost:5120/api/auth/admin/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ email: loginEmail, password }),
        })

        if (!res.ok) {
            return { success: false, error: 'Invalid email or password.' }
        }

        setEmail(loginEmail)
        setIsAuthenticated(true)
        return { success: true }
    }

    const logout = async () => {
        setIsAuthenticated(false)
        setEmail(null)
        await fetch('http://localhost:5120/api/auth/admin/logout', {
            method: 'POST',
            credentials: 'include',
        })
    }

    return (
        <AdminAuthContext.Provider value={{ isAuthenticated, isLoading, email, login, logout }}>
            {children}
        </AdminAuthContext.Provider>
    )
}

export function useAdminAuth() {
    const ctx = useContext(AdminAuthContext)
    if (!ctx) throw new Error('useAdminAuth must be used within AdminAuthProvider')
    return ctx
}