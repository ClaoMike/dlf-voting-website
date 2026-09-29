// jsdom has no <dialog> behaviour yet; Modal only needs showModal/close to toggle the open attribute.
HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
    this.open = true
}
HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
    this.open = false
    this.dispatchEvent(new Event('close'))
}
