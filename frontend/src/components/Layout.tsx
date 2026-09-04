import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import dlfLogo from '../assets/dlf-logo.svg'
import { useAuth } from '../context/AuthContext'
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
    const { isAuthenticated, logout } = useAuth()
    const navigate = useNavigate()
    const [showSignOutConfirm, setShowSignOutConfirm] = useState(false)

    const handleConfirmSignOut = async () => {
        setShowSignOutConfirm(false)
        await logout()
        navigate('/login/admin')
    }

    return (
        <div className="app-shell">
            <nav className="app-sidebar">
                <div className="sidebar-header">
                    <img src={dlfLogo} alt="DLF logo" className="sidebar-logo" />
                    <span className="sidebar-title">Voting System</span>
                </div>

                {isAuthenticated && (
                    <>
                        <button
                            className="sidebar-signout"
                            onClick={() => setShowSignOutConfirm(true)}
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
            </nav>
            <main className="app-content">
                <Outlet />
            </main>

            {showSignOutConfirm && (
                <ConfirmDialog
                    title="Sign out"
                    message="Are you sure you want to sign out?"
                    confirmLabel="Sign out"
                    onConfirm={handleConfirmSignOut}
                    onCancel={() => setShowSignOutConfirm(false)}
                />
            )}
        </div>
    )
}

export default Layout