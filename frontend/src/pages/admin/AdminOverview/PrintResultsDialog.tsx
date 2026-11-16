import { useId, useState } from 'react'
import Modal from '../../../components/Modal/Modal'
import { REPORT_SECTIONS } from './reportSections'
import type { ReportSection } from './types'

type PrintResultsDialogProps = {
    isPreparing: boolean
    error: string | null
    onPrint: (sections: Set<ReportSection>) => void
    onCancel: () => void
}

/** Lets the admin choose what goes into the printed results report. Everything is selected to begin with. */
function PrintResultsDialog({ isPreparing, error, onPrint, onCancel }: PrintResultsDialogProps) {
    const [selected, setSelected] = useState<Set<ReportSection>>(() => new Set(REPORT_SECTIONS.map((s) => s.id)))
    const titleId = useId()
    const hintId = useId()

    const toggle = (id: ReportSection) =>
        setSelected((current) => {
            const next = new Set(current)
            if (next.has(id)) next.delete(id)
            else next.add(id)
            return next
        })

    return (
        <Modal titleId={titleId} onClose={onCancel}>
            <h2 id={titleId} className="confirm-dialog-title">Print results</h2>
            <fieldset className="print-results-options" aria-describedby={hintId}>
                <legend className="dialog-label">Include in the report</legend>
                <p id={hintId} className="dialog-hint">
                    To keep a copy as a file, choose “Save as PDF” in the print window.
                </p>
                {REPORT_SECTIONS.map((section) => (
                    <label key={section.id} className="print-results-option">
                        <input
                            type="checkbox"
                            checked={selected.has(section.id)}
                            onChange={() => toggle(section.id)}
                        />
                        {section.label}
                    </label>
                ))}
            </fieldset>

            {error && <p className="dialog-error" role="alert">{error}</p>}

            <div className="confirm-dialog-actions">
                <button className="confirm-dialog-cancel" onClick={onCancel}>
                    Cancel
                </button>
                <button
                    className="confirm-dialog-confirm confirm-dialog-save"
                    disabled={selected.size === 0 || isPreparing}
                    onClick={() => onPrint(selected)}
                >
                    {isPreparing ? 'Preparing…' : 'Print'}
                </button>
            </div>
        </Modal>
    )
}

export default PrintResultsDialog
