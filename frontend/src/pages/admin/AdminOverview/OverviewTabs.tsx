import type { Tab } from './types'

type OverviewTabsProps = {
    activeTab: Tab
    onTabChange: (tab: Tab) => void
}

function OverviewTabs({ activeTab, onTabChange }: OverviewTabsProps) {
    return (
        <div className="overview-tabs">
            <button
                className={activeTab === 'all' ? 'overview-tab active' : 'overview-tab'}
                onClick={() => onTabChange('all')}
            >
                View all users
            </button>
            <button
                className={activeTab === 'voted' ? 'overview-tab active' : 'overview-tab'}
                onClick={() => onTabChange('voted')}
            >
                View users who voted
            </button>
        </div>
    )
}

export default OverviewTabs