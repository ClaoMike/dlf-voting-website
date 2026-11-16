import { createPortal } from 'react-dom'
import dlfLogo from '../../../assets/dlf-logo.svg'
import { COMPANY_NAME, VOTING_SYSTEM_WEBSITE_TITLE } from '../../../constants/strings'
import type { ReportSection, ReportUser, VoteReport } from './types'

type PrintableReportProps = {
    report: VoteReport
    sections: Set<ReportSection>
}

const percent = (part: number, whole: number) => (whole > 0 ? Math.round((part / whole) * 1000) / 10 : 0)

function UserTable({ users, caption }: { users: ReportUser[]; caption: string }) {
    if (users.length === 0) return <p>None.</p>

    // Employee columns only when the import filled them in for someone.
    const showEmployeeCode = users.some((u) => u.employeeCode)
    const showCompany = users.some((u) => u.companyCode)

    return (
        <table className="print-report-table">
            <caption className="visually-hidden">{caption}</caption>
            <thead>
            <tr>
                <th scope="col">#</th>
                <th scope="col">Name</th>
                <th scope="col">Username</th>
                {showEmployeeCode && <th scope="col">Employee no.</th>}
                {showCompany && <th scope="col">Company</th>}
            </tr>
            </thead>
            <tbody>
            {users.map((u, i) => (
                <tr key={u.username}>
                    <td>{i + 1}</td>
                    <td>{u.name ?? '—'}</td>
                    <td>{u.username}</td>
                    {showEmployeeCode && <td>{u.employeeCode ?? ''}</td>}
                    {showCompany && <td>{u.companyCode ?? ''}</td>}
                </tr>
            ))}
            </tbody>
        </table>
    )
}

/**
 * The results report, only visible when printing (the rest of the page is hidden then). It is rendered straight into
 * <body> so the print styles can hide the app around it.
 */
function PrintableReport({ report, sections }: PrintableReportProps) {
    const generatedAt = new Date(report.generatedAt).toLocaleString('en-GB', { dateStyle: 'long', timeStyle: 'short' })
    const turnout = percent(report.votedUsers, report.totalUsers)
    const votesCast = report.optionCounts.reduce((sum, o) => sum + o.count, 0)

    return createPortal(
        <article className="print-report" aria-label="Printable results report">
            <header className="print-report-header">
                <div className="print-report-brand">
                    <img src={dlfLogo} alt={COMPANY_NAME} className="print-report-logo" />
                </div>
                <h1>{VOTING_SYSTEM_WEBSITE_TITLE}: results</h1>
                <p>
                    {report.isVotingOpen
                        ? 'Voting was still open when this report was generated.'
                        : 'Voting was closed when this report was generated.'}
                </p>
            </header>

            {(sections.has('votedCount') || sections.has('turnout')) && (
                <section className="print-report-summary">
                    {sections.has('votedCount') && (
                        <div>
                            <h2>Users who voted</h2>
                            <p className="print-report-figure">{report.votedUsers} of {report.totalUsers}</p>
                        </div>
                    )}
                    {sections.has('turnout') && (
                        <div>
                            <h2>Turnout</h2>
                            <p className="print-report-figure">{turnout}%</p>
                        </div>
                    )}
                </section>
            )}

            {sections.has('optionCounts') && (
                <section>
                    <h2>Votes per option</h2>
                    <table className="print-report-table">
                        <caption className="visually-hidden">Votes per option</caption>
                        <thead>
                        <tr>
                            <th scope="col">Option</th>
                            <th scope="col" className="print-report-number">Votes</th>
                            <th scope="col" className="print-report-number">Share</th>
                            <th scope="col"><span className="visually-hidden">Bar</span></th>
                        </tr>
                        </thead>
                        <tbody>
                        {report.optionCounts.map((o) => (
                            <tr key={o.votingOptionId}>
                                <td>{o.votingOptionName}</td>
                                <td className="print-report-number">{o.count}</td>
                                <td className="print-report-number">{percent(o.count, votesCast)}%</td>
                                <td className="print-report-bar-cell">
                                    <span className="print-report-bar" style={{ width: `${percent(o.count, votesCast)}%` }} />
                                </td>
                            </tr>
                        ))}
                        </tbody>
                        <tfoot>
                        <tr>
                            <th scope="row">Total votes cast</th>
                            <td className="print-report-number">{votesCast}</td>
                            <td colSpan={2} />
                        </tr>
                        </tfoot>
                    </table>
                </section>
            )}

            {sections.has('votersList') && (
                <section>
                    <h2>Users who voted ({report.usersWhoVoted.length})</h2>
                    <UserTable users={report.usersWhoVoted} caption="Users who voted" />
                </section>
            )}

            {sections.has('nonVotersList') && (
                <section>
                    <h2>Users who did not vote ({report.usersWhoDidNotVote.length})</h2>
                    <UserTable users={report.usersWhoDidNotVote} caption="Users who did not vote" />
                </section>
            )}

            <footer className="print-report-footer">
                Generated on {generatedAt} by {report.generatedBy}
            </footer>
        </article>,
        document.body,
    )
}

export default PrintableReport
