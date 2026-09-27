import ConfirmDialog from '../../../components/ConfirmDialog/ConfirmDialog'
import EditVoteDialog from '../../../components/EditVoteDialog'
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
                        onEdit={actions.startEdit}
                        onDelete={actions.startDelete}
                        onPageChange={data.goToPage}
                    />
                )}
            </section>

            {actions.editingVote && (
                <EditVoteDialog
                    options={data.options}
                    initialOptionId={actions.editingVote.votingOptionId ?? ''}
                    onSave={actions.confirmEdit}
                    onCancel={actions.cancelEdit}
                />
            )}

            {actions.deletingVote && (
                <ConfirmDialog
                    title="Remove vote"
                    message={`Are you sure you want to remove ${actions.deletingVote.username}'s vote?`}
                    confirmLabel="Remove"
                    onConfirm={actions.confirmDelete}
                    onCancel={actions.cancelDelete}
                />
            )}
        </div>
    )
}

export default AdminOverview