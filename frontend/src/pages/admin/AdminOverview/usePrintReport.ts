import { useEffect, useState } from 'react'
import type { ReportSection, VoteReport } from './types'

const REPORT_API = '/api/votes/report'
const REPORT_ERROR = 'Could not load the results. Please try again.'

type PrintJob = { report: VoteReport; sections: Set<ReportSection> }

/**
 * "Print results": loads the report (read-only on the server), then shows it to the browser's print window.
 * The report stays in the page only while printing.
 */
export function usePrintReport() {
    const [isDialogOpen, setIsDialogOpen] = useState(false)
    const [isPreparing, setIsPreparing] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [job, setJob] = useState<PrintJob | null>(null)

    // Runs once the report is on the page. The title becomes the suggested file name for "Save as PDF".
    useEffect(() => {
        if (!job) return
        const previousTitle = document.title
        document.title = `Voting results ${job.report.generatedAt.slice(0, 10)}`
        const finish = () => setJob(null)
        window.addEventListener('afterprint', finish)
        window.print()
        return () => {
            window.removeEventListener('afterprint', finish)
            document.title = previousTitle
        }
    }, [job])

    const open = () => {
        setError(null)
        setIsDialogOpen(true)
    }

    const print = async (sections: Set<ReportSection>) => {
        setIsPreparing(true)
        setError(null)
        try {
            const res = await fetch(REPORT_API, { credentials: 'include' })
            if (!res.ok) {
                setError(REPORT_ERROR)
                return
            }
            const report: VoteReport = await res.json()
            setIsDialogOpen(false)
            setJob({ report, sections: new Set(sections) })
        } catch {
            setError(REPORT_ERROR)
        } finally {
            setIsPreparing(false)
        }
    }

    return {
        isDialogOpen,
        isPreparing,
        error,
        job,
        open,
        cancel: () => setIsDialogOpen(false),
        print,
    }
}
