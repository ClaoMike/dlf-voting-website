import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AdminOverview from './AdminOverview'
import type { PagedVotes, VoteReport, VoteStats } from './types'

const stats: VoteStats = {
    totalUsers: 4,
    votedUsers: 3,
    optionCounts: [
        { votingOptionId: 'a', votingOptionName: 'Option A', count: 2 },
        { votingOptionId: 'b', votingOptionName: 'A much longer option name that wraps onto several lines', count: 1 },
    ],
}

const votesPage: PagedVotes = { items: [], totalCount: 0, page: 1, pageSize: 25 }

const report: VoteReport = {
    generatedAt: '2026-11-16T15:30:00Z',
    generatedBy: 'boss-admin',
    isVotingOpen: false,
    totalUsers: 4,
    votedUsers: 3,
    optionCounts: stats.optionCounts,
    usersWhoVoted: [
        { name: 'Anna Berg', username: 'anna', employeeCode: '00042', companyCode: 'DLF1' },
        { name: 'Bo Holm', username: 'bo', employeeCode: null, companyCode: null },
        { name: null, username: 'nameless', employeeCode: null, companyCode: null },
    ],
    usersWhoDidNotVote: [{ name: 'Carl Dahl', username: 'carl', employeeCode: null, companyCode: null }],
}

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

// Stands in for the API and records every request, so the tests can check that printing only reads.
function mockApi({ reportStatus = 200 } = {}) {
    const requests: { url: string; method: string }[] = []
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
        requests.push({ url, method: init?.method ?? 'GET' })
        if (url === '/api/votes/stats') return json(stats)
        if (url === '/api/votes/report') return reportStatus === 200 ? json(report) : new Response(null, { status: reportStatus })
        if (url.startsWith('/api/votes?')) return json(votesPage)
        return new Response(null, { status: 404 })
    }))
    return requests
}

const printedReport = () => document.querySelector<HTMLElement>('.print-report')

async function openPrintDialog(user: ReturnType<typeof userEvent.setup>) {
    await user.click(await screen.findByRole('button', { name: 'Print results' }))
    return screen.getByRole('dialog', { name: 'Print results' })
}

describe('Admin overview', () => {
    let printSpy: ReturnType<typeof vi.fn>

    beforeEach(() => {
        printSpy = vi.fn()
        vi.stubGlobal('print', printSpy)
    })

    afterEach(() => {
        cleanup()
        vi.unstubAllGlobals()
    })

    describe('votes per option chart', () => {
        it('puts every bar in the same plot row, with the labels in a row of their own', async () => {
            mockApi()
            render(<AdminOverview />)

            const grid = await waitFor(() => {
                const el = document.querySelector<HTMLElement>('.vote-bar-grid')
                expect(el).not.toBeNull()
                return el!
            })
            const plots = grid.querySelectorAll('.vote-bar-plot')
            const labels = grid.querySelectorAll('.vote-bar-label')

            expect(plots).toHaveLength(2)
            // All bars first, then all labels: two grid rows, so a label's length can't move its bar.
            expect([...grid.children].map((c) => c.className)).toEqual([
                'vote-bar-plot', 'vote-bar-plot', 'vote-bar-label', 'vote-bar-label',
            ])
            expect(grid.style.getPropertyValue('--bar-columns')).toBe('2')
            expect([...labels].map((l) => l.textContent)).toEqual([
                'Option A', 'A much longer option name that wraps onto several lines',
            ])
            // Heights are relative to the tallest bar.
            const bars = grid.querySelectorAll<HTMLElement>('.vote-bar')
            expect([...bars].map((b) => b.style.height)).toEqual(['100%', '50%'])
        })
    })

    describe('Print results', () => {
        it('offers all five sections, all selected to begin with', async () => {
            mockApi()
            const user = userEvent.setup()
            render(<AdminOverview />)

            const dialog = await openPrintDialog(user)
            const boxes = within(dialog).getAllByRole('checkbox')

            expect(boxes.map((b) => b.closest('label')!.textContent)).toEqual([
                'Users who voted (count)',
                'Turnout',
                'Votes per option',
                'List of users who voted',
                'List of users who did not vote',
            ])
            boxes.forEach((b) => expect((b as HTMLInputElement).checked).toBe(true))
        })

        it('cannot print with nothing selected', async () => {
            mockApi()
            const user = userEvent.setup()
            render(<AdminOverview />)

            const dialog = await openPrintDialog(user)
            for (const box of within(dialog).getAllByRole('checkbox')) await user.click(box)

            expect((within(dialog).getByRole('button', { name: 'Print' }) as HTMLButtonElement).disabled).toBe(true)
        })

        it('prints only the selected sections, then removes the report again', async () => {
            const requests = mockApi()
            const user = userEvent.setup()
            render(<AdminOverview />)

            const dialog = await openPrintDialog(user)
            await user.click(within(dialog).getByLabelText('Turnout'))
            await user.click(within(dialog).getByLabelText('List of users who did not vote'))
            await user.click(within(dialog).getByRole('button', { name: 'Print' }))

            await waitFor(() => expect(printSpy).toHaveBeenCalledTimes(1))
            expect(screen.queryByRole('dialog')).toBeNull()

            const reportEl = printedReport()!
            expect(reportEl).not.toBeNull()
            const headings = within(reportEl).getAllByRole('heading').map((h) => h.textContent)
            expect(headings).toEqual([
                'Koncernvalg 2026: results',
                'Users who voted',
                'Votes per option',
                'Users who voted (3)',
            ])
            // The company logo first (named for screen readers), the generated-by line last.
            const logo = reportEl.querySelector<HTMLImageElement>('.print-report-brand img.print-report-logo')!
            expect(logo.alt).toBe('DLF')
            const footer = reportEl.lastElementChild!
            expect(footer.tagName).toBe('FOOTER')
            expect(footer.textContent).toMatch(/^Generated on 16 November 2026 at \d{2}:30 by boss-admin$/)

            expect(reportEl.textContent).toContain('3 of 4')
            expect(reportEl.textContent).toContain('Voting was closed')
            expect(reportEl.textContent).toContain('Total votes cast3')
            // Share of the votes cast, rounded to one decimal.
            expect(reportEl.textContent).toContain('66.7%')
            expect(reportEl.textContent).toContain('Anna Berg')
            expect(reportEl.textContent).toContain('00042')
            expect(reportEl.textContent).not.toContain('Carl Dahl')

            // Printing only reads: no request other than GETs was sent.
            expect(requests.every((r) => r.method === 'GET')).toBe(true)
            expect(requests.filter((r) => r.url === '/api/votes/report')).toHaveLength(1)

            window.dispatchEvent(new Event('afterprint'))
            await waitFor(() => expect(printedReport()).toBeNull())
        })

        it('keeps the dialog open with a message when the report cannot be loaded', async () => {
            mockApi({ reportStatus: 500 })
            const user = userEvent.setup()
            render(<AdminOverview />)

            const dialog = await openPrintDialog(user)
            await user.click(within(dialog).getByRole('button', { name: 'Print' }))

            expect((await within(dialog).findByRole('alert')).textContent).toContain('Could not load the results')
            expect(printSpy).not.toHaveBeenCalled()
            expect(printedReport()).toBeNull()
        })
    })
})
