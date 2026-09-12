import { NavLink } from 'react-router-dom'

const ADMIN_NAV_ITEMS = [
    { label: 'Overview', path: '/admin/overview' },
    { label: 'Voting Options', path: '/admin/voting_options' },
    { label: 'Users', path: '/admin/users' },
    { label: 'Administrators', path: '/admin/administrators' },
    { label: 'Settings', path: '/admin/settings' },
]

function AdminNav() {
    return (
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
    )
}

export default AdminNav