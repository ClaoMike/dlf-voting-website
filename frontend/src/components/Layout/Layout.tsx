import { useState } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAdminAuth } from '../../context/AdminAuthContext'
import { useUserAuth } from '../../context/UserAuthContext'
import ConfirmDialog from '../ConfirmDialog/ConfirmDialog'
import SidebarHeader from './SidebarHeader'
import AdminNav from './AdminNav'
import SidebarAuthActions from './SidebarAuthActions'
import './Layout.css'

function Layout() {
    const admin = useAdminAuth()
    const user = useUserAuth()
    const navigate = useNavigate()
    const location = useLocation()
    const [confirmingSignOutAs, setConfirmingSignOutAs] = useState<'admin' | 'user' | null>(null)

    const handleConfirmSignOut = async () => {
        if (confirmingSignOutAs === 'admin') {
            setConfirmingSignOutAs(null)
            await admin.logout()
            navigate('/login/admin')
        } else if (confirmingSignOutAs === 'user') {
            setConfirmingSignOutAs(null)
            await user.logout()
            navigate('/login')
        }
    }

    const handleSignOutAndLoginAsUser = async () => {
        await admin.logout()
        window.location.href = '/login'
    }

    return (
        <div className="app-shell">
            <nav className="app-sidebar">
                <SidebarHeader />

                {admin.isAuthenticated && <AdminNav />}

                <SidebarAuthActions
                    isAdmin={admin.isAuthenticated}
                    isUser={!admin.isAuthenticated && user.isAuthenticated}
                    isOnAdminLoginPage={
                        !admin.isAuthenticated && !user.isAuthenticated && location.pathname === '/login/admin'
                    }
                    onAdminSignOutClick={() => setConfirmingSignOutAs('admin')}
                    onSwitchToUserClick={handleSignOutAndLoginAsUser}
                    onUserSignOutClick={() => setConfirmingSignOutAs('user')}
                    onGoToUserLogin={() => navigate('/login')}
                />
            </nav>
            <main className="app-content">
                <Outlet />
            </main>

            {confirmingSignOutAs && (
                <ConfirmDialog
                    title="Sign out"
                    message="Are you sure you want to sign out?"
                    confirmLabel="Sign out"
                    onConfirm={handleConfirmSignOut}
                    onCancel={() => setConfirmingSignOutAs(null)}
                />
            )}
        </div>
    )
}

export default Layout