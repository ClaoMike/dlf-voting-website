import dlfLogo from '../../assets/dlf-logo.svg'
import { VOTING_SYSTEM_WEBSITE_TITLE } from '../../constants/strings'

function SidebarHeader() {
    return (
        <div className="sidebar-header">
            <img src={dlfLogo} alt="DLF logo" className="sidebar-logo" />
            <span className="sidebar-title">{VOTING_SYSTEM_WEBSITE_TITLE}</span>
        </div>
    )
}

export default SidebarHeader