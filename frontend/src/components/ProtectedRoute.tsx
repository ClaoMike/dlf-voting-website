import { Navigate, Outlet } from 'react-router-dom'
import { useAdminAuth } from '../context/AdminAuthContext'
import { useUserAuth } from '../context/UserAuthContext'

export function AdminProtectedRoute() {
    const { isAuthenticated, isLoading } = useAdminAuth()

    if (isLoading) return <div>Loading...</div>
    if (!isAuthenticated) return <Navigate to="/login/admin" replace />

    return <Outlet />
}

export function UserProtectedRoute() {
    const { isAuthenticated, isLoading } = useUserAuth()

    if (isLoading) return <div>Loading...</div>
    if (!isAuthenticated) return <Navigate to="/login" replace />

    return <Outlet />
}