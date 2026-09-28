import ConfirmDialog from '../../../components/ConfirmDialog/ConfirmDialog'
import OverviewCharts from './OverviewCharts'
import OverviewTabs from './OverviewTabs'
import VotesTable from './VotesTable'
import { useVotesData } from './useVotesData'
import { useVoteActions } from './useVoteActions'

import '../AdminVotingOptions/AdminVotingOptions.css'
import '../../../components/VoteBarChart/VoteBarChart.css'
import './AdminOverview.css'

function AdminOverview() {
    const data = useVotesData()
    const actions = useVoteActions(data.refresh)

    return (
        <div className="voting-options-page">
            <h1>Overview</h1>

            {data.stats && (
                <section className="admin-card" aria-labelledby="overview-results">
                    <h2 id="overview-results">Results so far</h2>
                    <OverviewCharts stats={data.stats} />
                </section>
            )}

            <section className="admin-card" aria-labelledby="overview-votes">
                <h2 id="overview-votes">Votes</h2>
                <OverviewTabs activeTab={data.tab} onTabChange={data.changeTab} />

                {(data.error || actions.error) && (
                    <p className="voting-options-error" role="alert">{data.error ?? actions.error}</p>
                )}

                {data.isLoading ? (
                    <p role="status">Loading…</p>
                ) : (
                    <VotesTable
                        votes={data.votes}
                        tab={data.tab}
                        page={data.page}
                        totalPages={data.totalPages}
                        onReset={actions.startReset}
                        onPageChange={data.goToPage}
                    />
                )}
            </section>

            {actions.resettingVote && (
                <ConfirmDialog
                    title="Reset vote"
                    message={`Are you sure you want to reset ${actions.resettingVote.username}'s vote?`}
                    confirmLabel="Reset"
                    onConfirm={actions.confirmReset}
                    onCancel={actions.cancelReset}
                />
            )}
        </div>
    )
}

export default AdminOverview