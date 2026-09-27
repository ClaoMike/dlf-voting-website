import { useCallback, useEffect, useRef, useState, type ChangeEvent } from 'react'
import type { ImportMode, ImportProgress, ImportSummary } from './types'

const API_BASE = '/api/users'
const POLL_INTERVAL_MS = 1000
// Survives a page reload, so an import that is still running (or finished while the page was away) isn't lost.
const PENDING_IMPORT_KEY = 'dlfvoting.pendingImport'

const XLSX_MIME = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

// The backend returns the result workbook as base64.
function downloadXlsx(base64: string, fileName: string) {
    const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0))
    const blob = new Blob([bytes], { type: XLSX_MIME })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    link.click()
    URL.revokeObjectURL(url)
}

type EmailImportResult = {
    created: { email: string; username: string; password: string }[]
    skipped: { email: string; reason: string }[]
    file: string | null
}

type EmployeeImportResult = {
    createdCount: number
    skippedCount: number
    warnings: { row: number; message: string }[]
    file: string
}

type ImportJob = {
    id: string
    status: 'running' | 'succeeded' | 'failed'
    processed: number
    total: number
    result: EmailImportResult | EmployeeImportResult | null
    message: string | null
}

type PendingImport = { id: string; mode: ImportMode; fileBaseName: string }

function readPending(): PendingImport | null {
    try {
        const raw = sessionStorage.getItem(PENDING_IMPORT_KEY)
        return raw ? (JSON.parse(raw) as PendingImport) : null
    } catch {
        return null
    }
}

function writePending(pending: PendingImport | null) {
    try {
        if (pending) sessionStorage.setItem(PENDING_IMPORT_KEY, JSON.stringify(pending))
        else sessionStorage.removeItem(PENDING_IMPORT_KEY)
    } catch {
        // Private mode etc.: the import still works, it just can't be resumed after a reload.
    }
}

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

/**
 * Imports run in the background on the server (they can take minutes); this starts one, polls its progress, and
 * downloads the Excel file with the new logins when it's done.
 */
export function useUserImport(onChanged: () => Promise<void>) {
    const [isImporting, setIsImporting] = useState(false)
    const [progress, setProgress] = useState<ImportProgress | null>(null)
    const [importSummary, setImportSummary] = useState<ImportSummary | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [showModeDialog, setShowModeDialog] = useState(false)
    const fileInputRef = useRef<HTMLInputElement>(null)
    const modeRef = useRef<ImportMode>('emails')
    const followingRef = useRef(false)
    // The page passes a new onChanged on every render; read the latest one so `follow` (and the resume below)
    // stays the same function and only runs once per page load.
    const onChangedRef = useRef(onChanged)
    useEffect(() => {
        onChangedRef.current = onChanged
    }, [onChanged])

    /** Polls the job until it finishes, then shows the summary and downloads the file. */
    const follow = useCallback(async (pending: PendingImport) => {
        if (followingRef.current) return
        followingRef.current = true
        setIsImporting(true)
        setError(null)
        setImportSummary(null)

        try {
            let job: ImportJob | null = null
            let failedPolls = 0
            while (true) {
                const res = await fetch(`${API_BASE}/imports/${pending.id}`, { credentials: 'include' }).catch(() => null)
                if (res?.ok) {
                    job = (await res.json()) as ImportJob
                    failedPolls = 0
                    setProgress({ processed: job.processed, total: job.total })
                    if (job.status !== 'running') break
                } else if (res && (res.status === 404 || res.status === 401)) {
                    setError(res.status === 404
                        ? 'The result of this import is no longer available. Check the user list before importing again.'
                        : 'Your session ended during the import. Sign in again to see the result.')
                    if (res.status === 404) writePending(null)
                    return
                } else if (++failedPolls >= 30) {
                    // About 30 s without an answer. The import keeps running; reloading the page resumes it.
                    setError('Lost contact with the server. Reload the page to continue following the import.')
                    return
                }
                await sleep(POLL_INTERVAL_MS)
            }

            if (job.status === 'failed') {
                setError(job.message ?? 'The import failed. No users were created.')
            } else if (pending.mode === 'employees') {
                const data = job.result as EmployeeImportResult
                setImportSummary({ created: data.createdCount, skipped: data.skippedCount, warnings: data.warnings })
                if (data.createdCount > 0) {
                    downloadXlsx(data.file, `${pending.fileBaseName}-with-logins-${new Date().toISOString().slice(0, 10)}.xlsx`)
                }
            } else {
                const data = job.result as EmailImportResult
                setImportSummary({ created: data.created.length, skipped: data.skipped.length, warnings: [] })
                if (data.file) {
                    downloadXlsx(data.file, `imported-users-${new Date().toISOString().slice(0, 10)}.xlsx`)
                }
            }

            // The file is saved (or there was nothing to save): the server can forget the passwords now.
            writePending(null)
            await fetch(`${API_BASE}/imports/${pending.id}`, { method: 'DELETE', credentials: 'include' }).catch(() => {})
            await onChangedRef.current()
        } finally {
            followingRef.current = false
            setIsImporting(false)
            setProgress(null)
        }
    }, [])

    // Resume an import that was still running (or unfetched) when the page was reloaded.
    useEffect(() => {
        const pending = readPending()
        if (pending) void follow(pending)
    }, [follow])

    const handleFileSelected = async (e: ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0]
        if (fileInputRef.current) fileInputRef.current.value = ''
        if (!file) return

        const mode = modeRef.current
        setError(null)
        setImportSummary(null)
        setIsImporting(true)

        try {
            const formData = new FormData()
            formData.append('file', file)
            const res = await fetch(`${API_BASE}/${mode === 'employees' ? 'import-employees' : 'bulk-import'}`, {
                method: 'POST',
                credentials: 'include',
                body: formData,
            })

            if (res.status !== 202) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to import users.')
                setIsImporting(false)
                return
            }

            const job = (await res.json()) as ImportJob
            const pending: PendingImport = { id: job.id, mode, fileBaseName: file.name.replace(/\.xlsx$/i, '') }
            writePending(pending)
            await follow(pending)
        } catch {
            setError('Failed to import users.')
            setIsImporting(false)
        }
    }

    const chooseMode = (mode: ImportMode) => {
        modeRef.current = mode
        setShowModeDialog(false)
        fileInputRef.current?.click()
    }

    return {
        isImporting,
        progress,
        importSummary,
        error,
        fileInputRef,
        showModeDialog,
        openModeDialog: () => setShowModeDialog(true),
        cancelModeDialog: () => setShowModeDialog(false),
        chooseMode,
        handleFileSelected,
    }
}
