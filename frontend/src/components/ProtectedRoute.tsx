import { Navigate, Outlet } from 'react-router-dom'
import { useAdminAuth } from '../context/useAdminAuth'
import { useUserAuth } from '../context/useUserAuth'

export function AdminProtectedRoute() {
    const { isAuthenticated, isLoading } = useAdminAuth()

    if (isLoading) return <p role="status">Loading…</p>
    if (!isAuthenticated) return <Navigate to="/login/admin" replace />

    return <Outlet />
}

export function UserProtectedRoute() {
    const { isAuthenticated, isLoading } = useUserAuth()

    if (isLoading) return <p role="status">Loading…</p>
    if (!isAuthenticated) return <Navigate to="/login" replace />

    return <Outlet />
}