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

            {data.stats && <OverviewCharts stats={data.stats} />}

            <OverviewTabs activeTab={data.tab} onTabChange={data.changeTab} />

            {(data.error || actions.error) && (
                <p className="voting-options-error">{data.error ?? actions.error}</p>
            )}

            {data.isLoading ? (
                <p>Loading...</p>
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
                    message={`Are you sure you want to remove ${actions.deletingVote.email}'s vote?`}
                    confirmLabel="Remove"
                    onConfirm={actions.confirmDelete}
                    onCancel={actions.cancelDelete}
                />
            )}
        </div>
    )
}

export default AdminOverview