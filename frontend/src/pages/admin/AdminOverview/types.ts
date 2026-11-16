export type VoteRow = {
    userId: string
    username: string
    firstName: string | null
    lastName: string | null
    hasVoted: boolean
}

export type PagedVotes = {
    items: VoteRow[]
    totalCount: number
    page: number
    pageSize: number
}

export type OptionVoteCount = {
    votingOptionId: string
    votingOptionName: string
    count: number
}

export type VoteStats = {
    totalUsers: number
    votedUsers: number
    optionCounts: OptionVoteCount[]
}

export type Tab = 'all' | 'voted'

export type ReportUser = {
    name: string | null
    username: string
    employeeCode: string | null
    companyCode: string | null
}

export type VoteReport = {
    generatedAt: string
    generatedBy: string
    isVotingOpen: boolean
    totalUsers: number
    votedUsers: number
    optionCounts: OptionVoteCount[]
    usersWhoVoted: ReportUser[]
    usersWhoDidNotVote: ReportUser[]
}

/** The parts of the printed report the admin can switch on and off. */
export type ReportSection = 'votedCount' | 'turnout' | 'optionCounts' | 'votersList' | 'nonVotersList'
