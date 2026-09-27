import type { Tab } from './types'

type OverviewTabsProps = {
    activeTab: Tab
    onTabChange: (tab: Tab) => void
}

/** Two filter buttons for the table below; aria-pressed tells screen readers which one is on. */
function OverviewTabs({ activeTab, onTabChange }: OverviewTabsProps) {
    return (
        <div className="overview-tabs" role="group" aria-label="Show">
            <button className="overview-tab" aria-pressed={activeTab === 'all'} onClick={() => onTabChange('all')}>
                All users
            </button>
            <button className="overview-tab" aria-pressed={activeTab === 'voted'} onClick={() => onTabChange('voted')}>
                Users who voted
            </button>
        </div>
    )
}

export default OverviewTabs
