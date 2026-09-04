import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'

type AuthContextType = {
    isAuthenticated: boolean
    isLoading: boolean
    login: () => void
    logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
    const [isAuthenticated, setIsAuthenticated] = useState(false)
    const [isLoading, setIsLoading] = useState(true)

    const checkSession = async () => {
        try {
            const res = await fetch('http://localhost:5120/api/auth/admin/me', {
                credentials: 'include',
            })
            setIsAuthenticated(res.ok)
        } catch {
            setIsAuthenticated(false)
        } finally {
            setIsLoading(false)
        }
    }

    useEffect(() => {
        checkSession()
    }, [])

    const login = () => setIsAuthenticated(true)

    const logout = async () => {
        await fetch('http://localhost:5120/api/auth/admin/logout', {
            method: 'POST',
            credentials: 'include',
        })
        setIsAuthenticated(false)
    }

    return (
        <AuthContext.Provider value={{ isAuthenticated, isLoading, login, logout }}>
            {children}
        </AuthContext.Provider>
    )
}

export function useAuth() {
    const ctx = useContext(AuthContext)
    if (!ctx) throw new Error('useAuth must be used within AuthProvider')
    return ctx
}