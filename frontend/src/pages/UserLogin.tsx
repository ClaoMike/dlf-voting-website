import { Navigate } from 'react-router-dom'
import LoginForm from '../components/LoginForm/LoginForm'
import { useAdminAuth } from '../context/AdminAuthContext'
import { useUserAuth } from '../context/UserAuthContext'

function UserLogin() {
    const { login, sessionExpired } = useUserAuth()
    const { isAuthenticated: isAdminAuthenticated, isLoading: isAdminLoading } = useAdminAuth()

    if (isAdminLoading) return <p role="status">Loading…</p>
    if (isAdminAuthenticated) return <Navigate to="/admin/overview" replace />

    return <LoginForm
            title="Sign in to vote"
            intro="Use the username and password you were given."
            notice={sessionExpired ? 'You were signed out after 10 minutes without activity. Please sign in again.' : undefined}
            login={login}
            redirectTo="/welcome"
        />
}

export default UserLogin