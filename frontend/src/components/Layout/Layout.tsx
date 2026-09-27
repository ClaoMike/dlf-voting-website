import { useEffect, useRef, useState } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAdminAuth } from '../../context/AdminAuthContext'
import { useUserAuth } from '../../context/UserAuthContext'
import ConfirmDialog from '../ConfirmDialog/ConfirmDialog'
import SessionTimeoutDialog from '../SessionTimeoutDialog/SessionTimeoutDialog'
import SidebarHeader from './SidebarHeader'
import AdminNav from './AdminNav'
import SidebarAuthActions from './SidebarAuthActions'
import GrassBand from '../Decor/GrassBand'
import { VOTING_SYSTEM_WEBSITE_TITLE } from '../../constants/strings'
import './Layout.css'

const PAGE_TITLES: Record<string, string> = {
    '/': 'Sign in',
    '/login': 'Sign in',
    '/login/admin': 'Administrator sign in',
    '/welcome': 'Vote',
    '/admin/overview': 'Overview',
    '/admin/voting_options': 'Voting options',
    '/admin/users': 'Users',
    '/admin/administrators': 'Administrators',
    '/admin/settings': 'Settings',
}

// The voter pages (sign in and voting) get a strip of grass along the bottom, under the sidebar too.
const GRASS_PATHS = new Set(['/', '/login', '/login/admin', '/welcome'])

function Layout() {
    const admin = useAdminAuth()
    const user = useUserAuth()
    const navigate = useNavigate()
    const location = useLocation()
    const [confirmingSignOutAs, setConfirmingSignOutAs] = useState<'admin' | 'user' | null>(null)
    const previousPath = useRef(location.pathname)

    // Each page gets its own tab title, so people (and screen readers) know where they are.
    useEffect(() => {
        const page = PAGE_TITLES[location.pathname]
        document.title = page ? `${page} – ${VOTING_SYSTEM_WEBSITE_TITLE}` : VOTING_SYSTEM_WEBSITE_TITLE
    }, [location.pathname])

    // After moving to another page inside the app, put focus on its heading so screen readers announce the new
    // page (a full page load does this by itself). Not on the first load.
    useEffect(() => {
        if (previousPath.current === location.pathname) return
        previousPath.current = location.pathname

        const heading = document.querySelector<HTMLElement>('#main h1')
        if (heading) {
            heading.tabIndex = -1
            heading.focus()
        } else {
            document.getElementById('main')?.focus()
        }
    }, [location.pathname])

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

    // Whichever session is about to time out (the admin one first, if both are).
    const expiringSession =
        admin.sessionWarningSecondsLeft !== null ? { ...admin, loginPath: '/login/admin' }
            : user.sessionWarningSecondsLeft !== null ? { ...user, loginPath: '/login' }
                : null

    const handleSignOutAndLoginAsUser = async () => {
        await admin.logout()
        window.location.href = '/login'
    }

    return (
        <div className="app-shell">
            <a className="skip-link" href="#main">Skip to main content</a>
            <aside className="app-sidebar">
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
            </aside>
            <main id="main" className="app-content" tabIndex={-1}>
                <Outlet />
            </main>
            {GRASS_PATHS.has(location.pathname) && <GrassBand />}

            {expiringSession && (
                <SessionTimeoutDialog
                    secondsLeft={expiringSession.sessionWarningSecondsLeft!}
                    onStaySignedIn={() => void expiringSession.staySignedIn()}
                    onSignOut={async () => {
                        await expiringSession.logout()
                        navigate(expiringSession.loginPath)
                    }}
                />
            )}

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