import ProgressRing from '../../../components/ProgressRing'
import VoteBarChart from '../../../components/VoteBarChart/VoteBarChart'
import type { VoteStats } from './types'

type OverviewChartsProps = {
    stats: VoteStats
}

function OverviewCharts({ stats }: OverviewChartsProps) {
    const percentage = stats.totalUsers > 0 ? Math.round((stats.votedUsers / stats.totalUsers) * 100) : 0

    return (
        <div className="overview-charts">
            <ProgressRing
                value={stats.votedUsers}
                max={stats.totalUsers}
                label={`${stats.votedUsers} / ${stats.totalUsers}`}
            />
            <ProgressRing value={stats.votedUsers} max={stats.totalUsers} label={`${percentage}%`} />
            <VoteBarChart data={stats.optionCounts.map((o) => ({ name: o.votingOptionName, count: o.count }))} />
        </div>
    )
}

export default OverviewCharts