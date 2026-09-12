type SidebarAuthActionsProps = {
    isAdmin: boolean
    isUser: boolean
    isOnAdminLoginPage: boolean
    onAdminSignOutClick: () => void
    onSwitchToUserClick: () => void
    onUserSignOutClick: () => void
    onGoToUserLogin: () => void
}

function SidebarAuthActions(
    {
        isAdmin,
        isUser,
        isOnAdminLoginPage,
        onAdminSignOutClick,
        onSwitchToUserClick,
        onUserSignOutClick,
        onGoToUserLogin,
    }: SidebarAuthActionsProps) {
    
    if (isAdmin) {
        return (
            <div className="sidebar-signout-row">
                <button className="sidebar-signout" onClick={onAdminSignOutClick}>
                    Sign out
                </button>
                <button className="sidebar-signout" onClick={onSwitchToUserClick}>
                    Sign out and log in as a user
                </button>
            </div>
        )
    }

    if (isUser) {
        return (
            <button className="sidebar-signout" onClick={onUserSignOutClick}>
                Sign out
            </button>
        )
    }

    if (isOnAdminLoginPage) {
        return (
            <button className="sidebar-switch-login" onClick={onGoToUserLogin}>
                Trying to log in as a user?
            </button>
        )
    }

    return null
}

export default SidebarAuthActions