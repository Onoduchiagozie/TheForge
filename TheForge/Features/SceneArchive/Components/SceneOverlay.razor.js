// SceneOverlay.razor.js
// Client-side typewriter ("archive decryption") playback. The full Chronicle text is already
// on the client (passed once via JS interop) — this module reveals it with setInterval only.
// No further server round-trips happen during playback, per the master prompt's "avoid
// unnecessary SignalR updates" / "maintain 60fps" performance requirements.

let intervalId = null;

export function startTypewriter(elementId, fullText, charsPerTick, tickMs) {
    stopTypewriter();

    const el = document.getElementById(elementId);
    if (!el || !fullText) return;

    el.textContent = '';
    let i = 0;
    const step = charsPerTick > 0 ? charsPerTick : 3;
    const interval = tickMs > 0 ? tickMs : 18;

    intervalId = setInterval(() => {
        i += step;
        el.textContent = fullText.slice(0, i);
        if (i >= fullText.length) {
            clearInterval(intervalId);
            intervalId = null;
        }
    }, interval);
}

export function stopTypewriter() {
    if (intervalId !== null) {
        clearInterval(intervalId);
        intervalId = null;
    }
}

export function focusElement(elementId) {
    const el = document.getElementById(elementId);
    if (el) el.focus();
}
