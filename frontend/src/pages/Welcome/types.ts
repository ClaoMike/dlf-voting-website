export type VotingOption = {
    id: string
    name: string
    createdAt: string
}

export type MyVote = {
    hasVoted: boolean
    votingOptionId: string | null
    votingOptionName: string | null
    updatedAt: string | null
}