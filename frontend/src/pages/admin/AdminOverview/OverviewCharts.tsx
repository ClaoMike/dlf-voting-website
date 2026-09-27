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
            <figure className="overview-chart">
                <ProgressRing
                    value={stats.votedUsers}
                    max={stats.totalUsers}
                    label={`${stats.votedUsers} / ${stats.totalUsers}`}
                    description={`${stats.votedUsers} of ${stats.totalUsers} users have voted`}
                />
                <figcaption>Users who voted</figcaption>
            </figure>
            <figure className="overview-chart">
                <ProgressRing
                    value={stats.votedUsers}
                    max={stats.totalUsers}
                    label={`${percentage}%`}
                    description={`Turnout ${percentage}%`}
                />
                <figcaption>Turnout</figcaption>
            </figure>
            <figure className="overview-chart">
                <VoteBarChart data={stats.optionCounts.map((o) => ({ name: o.votingOptionName, count: o.count }))} />
                <figcaption>Votes per option</figcaption>
            </figure>
        </div>
    )
}

export default OverviewCharts
