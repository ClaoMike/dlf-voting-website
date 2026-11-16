import type { ReportSection } from './types'

// In the order they appear in the dialog and in the printed report.
export const REPORT_SECTIONS: { id: ReportSection; label: string }[] = [
    { id: 'votedCount', label: 'Users who voted (count)' },
    { id: 'turnout', label: 'Turnout' },
    { id: 'optionCounts', label: 'Votes per option' },
    { id: 'votersList', label: 'List of users who voted' },
    { id: 'nonVotersList', label: 'List of users who did not vote' },
]
