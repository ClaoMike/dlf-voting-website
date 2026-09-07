import { Navigate } from 'react-router-dom'
import LoginForm from '../components/LoginForm'
import { useAdminAuth } from '../context/AdminAuthContext'
import { useUserAuth } from '../context/UserAuthContext'

function AdminLogin() {
    const { login } = useAdminAuth()
    const { isAuthenticated: isUserAuthenticated, isLoading: isUserLoading } = useUserAuth()

    if (isUserLoading) return <div>Loading...</div>
    if (isUserAuthenticated) return <Navigate to="/welcome" replace />

    return <LoginForm title="Administration" login={login} redirectTo="/admin/overview" />
}

export default AdminLogin