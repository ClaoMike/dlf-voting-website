import { Outlet } from 'react-router-dom'
import dlfLogo from '../assets/dlf-logo.svg'
import './Layout.css'

function Layout() {
    return (
        <div className="app-shell">
            <nav className="app-sidebar">
                <div className="sidebar-header">
                    <img src={dlfLogo} alt="DLF logo" className="sidebar-logo" />
                    <span className="sidebar-title">Voting System</span>
                </div>
                {/* Page-specific nav content will go here later */}
            </nav>
            <main className="app-content">
                <Outlet />
            </main>
        </div>
    )
}

export default Layout