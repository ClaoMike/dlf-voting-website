import { Navigate } from 'react-router-dom'
import LoginForm from '../components/LoginForm/LoginForm'
import { useAdminAuth } from '../context/useAdminAuth'
import { useUserAuth } from '../context/useUserAuth'

function AdminLogin() {
    const { login, sessionExpired } = useAdminAuth()
    const { isAuthenticated: isUserAuthenticated, isLoading: isUserLoading } = useUserAuth()

    if (isUserLoading) return <p role="status">Loading…</p>
    if (isUserAuthenticated) return <Navigate to="/welcome" replace />

    return <LoginForm
            title="Administration"
            notice={sessionExpired ? 'You were signed out after 10 minutes without activity. Please sign in again.' : undefined}
            login={login}
            redirectTo="/admin/overview"
        />
}

export default AdminLogin