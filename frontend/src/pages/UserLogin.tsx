import { Navigate } from 'react-router-dom'
import LoginForm from '../components/LoginForm/LoginForm'
import { useAdminAuth } from '../context/AdminAuthContext'
import { useUserAuth } from '../context/UserAuthContext'

function UserLogin() {
    const { login } = useUserAuth()
    const { isAuthenticated: isAdminAuthenticated, isLoading: isAdminLoading } = useAdminAuth()

    if (isAdminLoading) return <div>Loading...</div>
    if (isAdminAuthenticated) return <Navigate to="/admin/overview" replace />

    return <LoginForm title="Sign in" login={login} redirectTo="/welcome" />
}

export default UserLogin