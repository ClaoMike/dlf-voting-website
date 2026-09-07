import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'

type LoginResult = { success: boolean; error?: string }

type UserAuthContextType = {
  isAuthenticated: boolean
  isLoading: boolean
  email: string | null
  login: (email: string, password: string) => Promise<LoginResult>
  logout: () => Promise<void>
}

const UserAuthContext = createContext<UserAuthContextType | undefined>(undefined)

export function UserAuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [isLoading, setIsLoading] = useState(true)
  const [email, setEmail] = useState<string | null>(null)

  const checkSession = async () => {
    try {
      const res = await fetch('http://localhost:5120/api/auth/user/me', {
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
    const res = await fetch('http://localhost:5120/api/auth/user/login', {
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
    await fetch('http://localhost:5120/api/auth/user/logout', {
      method: 'POST',
      credentials: 'include',
    })
    setIsAuthenticated(false)
    setEmail(null)
  }

  return (
    <UserAuthContext.Provider value={{ isAuthenticated, isLoading, email, login, logout }}>
      {children}
    </UserAuthContext.Provider>
  )
}

export function useUserAuth() {
  const ctx = useContext(UserAuthContext)
  if (!ctx) throw new Error('useUserAuth must be used within UserAuthProvider')
  return ctx
}