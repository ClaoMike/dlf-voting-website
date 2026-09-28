import { createContext, useContext } from 'react'

export type LoginResult = { success: boolean; error?: string }

export type UserAuthContextType = {
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

// Kept apart from UserAuthProvider so that file only exports a component (needed for fast refresh).
export const UserAuthContext = createContext<UserAuthContextType | undefined>(undefined)

export function useUserAuth() {
    const ctx = useContext(UserAuthContext)
    if (!ctx) throw new Error('useUserAuth must be used within UserAuthProvider')
    return ctx
}
