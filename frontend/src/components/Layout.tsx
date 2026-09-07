import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
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

    return (
        <div className="app-shell">
            <nav className="app-sidebar">
                <div className="sidebar-header">
                    <img src={dlfLogo} alt="DLF logo" className="sidebar-logo" />
                    <span className="sidebar-title">Voting System</span>
                </div>

                {admin.isAuthenticated && (
                    <>
                        <button
                            className="sidebar-signout"
                            onClick={() => setConfirmingSignOutAs('admin')}
                        >
                            Sign out
                        </button>

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