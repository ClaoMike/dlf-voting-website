import { useRef, useState, type ChangeEvent } from 'react'
import type { ImportMode, ImportSummary } from './types'

const API_BASE = '/api/users'

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

type EmailImportResponse = {
    created: { email: string; username: string; password: string }[]
    skipped: { email: string; reason: string }[]
    file: string | null
}

type EmployeeImportResponse = {
    createdCount: number
    skippedCount: number
    warnings: { row: number; message: string }[]
    file: string
}

export function useUserImport(onChanged: () => Promise<void>) {
    const [isImporting, setIsImporting] = useState(false)
    const [importSummary, setImportSummary] = useState<ImportSummary | null>(null)
    const [error, setError] = useState<string | null>(null)
    const [showModeDialog, setShowModeDialog] = useState(false)
    const fileInputRef = useRef<HTMLInputElement>(null)
    const modeRef = useRef<ImportMode>('emails')

    const today = new Date().toISOString().slice(0, 10)

    const importEmails = async (file: File) => {
        const formData = new FormData()
        formData.append('file', file)

        const res = await fetch(`${API_BASE}/bulk-import`, {
            method: 'POST',
            credentials: 'include',
            body: formData,
        })

        if (!res.ok) {
            const body = await res.json().catch(() => null)
            setError(body?.message ?? 'Failed to import users.')
            return
        }

        const data: EmailImportResponse = await res.json()

        setImportSummary({
            created: data.created.length,
            skipped: data.skipped.length,
            warnings: [],
        })

        if (data.file) {
            downloadXlsx(data.file, `imported-users-${today}.xlsx`)
        }
    }

    const importEmployees = async (file: File) => {
        const formData = new FormData()
        formData.append('file', file)

        const res = await fetch(`${API_BASE}/import-employees`, {
            method: 'POST',
            credentials: 'include',
            body: formData,
        })

        if (!res.ok) {
            const body = await res.json().catch(() => null)
            setError(body?.message ?? 'Failed to import employees.')
            return
        }

        const data: EmployeeImportResponse = await res.json()

        setImportSummary({ created: data.createdCount, skipped: data.skippedCount, warnings: data.warnings })

        if (data.createdCount > 0) {
            const baseName = file.name.replace(/\.xlsx$/i, '')
            downloadXlsx(data.file, `${baseName}-with-logins-${today}.xlsx`)
        }
    }

    const handleFileSelected = async (e: ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0]
        if (!file) return

        setIsImporting(true)
        setError(null)
        setImportSummary(null)

        try {
            if (modeRef.current === 'employees') {
                await importEmployees(file)
            } else {
                await importEmails(file)
            }
            await onChanged()
        } catch {
            setError('Failed to import users.')
        } finally {
            setIsImporting(false)
            if (fileInputRef.current) fileInputRef.current.value = ''
        }
    }

    const chooseMode = (mode: ImportMode) => {
        modeRef.current = mode
        setShowModeDialog(false)
        fileInputRef.current?.click()
    }

    return {
        isImporting,
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
