import { useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import dlfLogo from '../assets/dlf-logo.svg'
import { useAdminAuth } from '../context/AdminAuthContext'
import { useUserAuth } from '../context/UserAuthContext'
import ConfirmDialog from './ConfirmDialog'
import './Layout.css'

const ADMIN_NAV_ITEMS = [
    { label: 'Overview', path: '/admin/overview' },
    { label: 'Voting Options', path: '/admin/voting_options' },
    { label: 'Users', path: '/admin/users' },
    { label: 'Administrators', path: '/admin/administrators' },
    { label: 'Settings', path: '/admin/settings' },
]

function Layout() {
    const admin = useAdminAuth()
    const user = useUserAuth()
    const navigate = useNavigate()
    const location = useLocation()
    const [confirmingSignOutAs, setConfirmingSignOutAs] = useState<'admin' | 'user' | null>(null)

    const handleConfirmSignOut = () => {
        if (confirmingSignOutAs === 'admin') {
            setConfirmingSignOutAs(null)
            admin.logout()
            navigate('/login/admin')
        } else if (confirmingSignOutAs === 'user') {
            setConfirmingSignOutAs(null)
            user.logout()
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
                <div className="sidebar-header">
                    <img src={dlfLogo} alt="DLF logo" className="sidebar-logo" />
                    <span className="sidebar-title">Voting System</span>
                </div>

                {admin.isAuthenticated && (
                    <>
                        <div className="sidebar-signout-row">
                            <button
                                className="sidebar-signout"
                                onClick={() => setConfirmingSignOutAs('admin')}
                            >
                                Sign out
                            </button>
                            <button className="sidebar-signout" onClick={handleSignOutAndLoginAsUser}>
                                Sign out and log in as a user
                            </button>
                        </div>

                        <ul className="sidebar-nav">
                            {ADMIN_NAV_ITEMS.map((item) => (
                                <li key={item.path}>
                                    <NavLink
                                        to={item.path}
                                        className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
                                    >
                                        {item.label}
                                    </NavLink>
                                </li>
                            ))}
                        </ul>
                    </>
                )}

                {!admin.isAuthenticated && user.isAuthenticated && (
                    <button
                        className="sidebar-signout"
                        onClick={() => setConfirmingSignOutAs('user')}
                    >
                        Sign out
                    </button>
                )}

                {!admin.isAuthenticated && !user.isAuthenticated && location.pathname === '/login/admin' && (
                    <button className="sidebar-switch-login" onClick={() => navigate('/login')}>
                        Trying to log in as a user?
                    </button>
                )}
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