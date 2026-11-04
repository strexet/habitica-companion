// Let validation reflow finish before revealing the control that already owns
// focus. Never pull the viewport back after the user moves to another control.
export async function revealFocusedElement(element) {
  if (!element?.isConnected) return;
  await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  if (!element.isConnected || element.matches(':disabled') || document.activeElement !== element) return;
  element.scrollIntoView({ block: 'center', inline: 'nearest', behavior: 'instant' });
}
