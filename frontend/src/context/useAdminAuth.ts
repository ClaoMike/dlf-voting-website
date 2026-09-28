import { createContext, useContext } from 'react'

export type LoginResult = { success: boolean; error?: string }

export type AdminAuthContextType = {
    isAuthenticated: boolean
    isLoading: boolean
    // True when the session ended because of inactivity (the login page says so).
    sessionExpired: boolean
    // Seconds until the session ends, while the "Are you still there?" warning should show; null otherwise.
    sessionWarningSecondsLeft: number | null
    staySignedIn: () => Promise<void>
    username: string | null
    login: (username: string, password: string) => Promise<LoginResult>
    logout: () => Promise<void>
}

// Kept apart from AdminAuthProvider so that file only exports a component (needed for fast refresh).
export const AdminAuthContext = createContext<AdminAuthContextType | undefined>(undefined)

export function useAdminAuth() {
    const ctx = useContext(AdminAuthContext)
    if (!ctx) throw new Error('useAdminAuth must be used within AdminAuthProvider')
    return ctx
}
