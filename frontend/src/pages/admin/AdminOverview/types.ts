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