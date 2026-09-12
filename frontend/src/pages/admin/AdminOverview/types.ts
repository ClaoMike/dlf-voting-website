export type VoteRow = {
    userId: string
    email: string
    votingOptionId: string | null
    votingOptionName: string | null
    updatedAt: string | null
}

export type PagedVotes = {
    items: VoteRow[]
    totalCount: number
    page: number
    pageSize: number
}

export type VotingOption = {
    id: string
    name: string
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