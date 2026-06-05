const queueAddAnchors = new Map();
const maxAnchorAgeMs = 750;

export function captureQueueAddScrollAnchor(correctionId, anchorElementId) {
  if (!correctionId) {
    return;
  }

  const anchor = anchorElementId ? document.getElementById(anchorElementId) : null;
  queueAddAnchors.set(correctionId, {
    anchorElementId,
    capturedAt: performance.now(),
    scrollY: window.scrollY,
    top: anchor ? anchor.getBoundingClientRect().top : null
  });
}

export function applyQueueAddScrollAnchor(correctionId) {
  const anchorState = queueAddAnchors.get(correctionId);
  queueAddAnchors.delete(correctionId);
  if (!anchorState || performance.now() - anchorState.capturedAt > maxAnchorAgeMs) {
    return;
  }

  if (anchorState.top !== null && anchorState.anchorElementId) {
    const anchor = document.getElementById(anchorState.anchorElementId);
    if (!anchor) {
      return;
    }

    const delta = anchor.getBoundingClientRect().top - anchorState.top;
    if (Number.isFinite(delta) && Math.abs(delta) > 0.5) {
      window.scrollBy({ top: delta, left: 0, behavior: "auto" });
    }

    return;
  }

  if (Number.isFinite(anchorState.scrollY)) {
    window.scrollTo({ top: anchorState.scrollY, left: window.scrollX, behavior: "auto" });
  }
}

export function discardQueueAddScrollAnchor(correctionId) {
  queueAddAnchors.delete(correctionId);
}
