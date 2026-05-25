const states = new WeakMap();

export function initializeTaskReorder(root, dotNetReference) {
  if (!root || states.has(root)) {
    return;
  }

  const state = {
    root,
    dotNetReference,
    drag: null,
    onPointerDown: event => handlePointerDown(state, event),
    onPointerMove: event => handlePointerMove(state, event),
    onPointerUp: event => finishPointerDrag(state, event),
    onPointerCancel: () => clearPointerDrag(state)
  };

  root.addEventListener("pointerdown", state.onPointerDown);
  root.addEventListener("pointermove", state.onPointerMove);
  root.addEventListener("pointerup", state.onPointerUp);
  root.addEventListener("pointercancel", state.onPointerCancel);
  states.set(root, state);
}

export function disposeTaskReorder(root) {
  const state = states.get(root);
  if (!state) {
    return;
  }

  root.removeEventListener("pointerdown", state.onPointerDown);
  root.removeEventListener("pointermove", state.onPointerMove);
  root.removeEventListener("pointerup", state.onPointerUp);
  root.removeEventListener("pointercancel", state.onPointerCancel);
  clearDropTarget(state);
  states.delete(root);
}

function handlePointerDown(state, event) {
  if (event.button !== 0) {
    return;
  }

  const handle = event.target.closest(".task-drag-handle");
  const card = handle?.closest("[data-task-id][data-task-type]");
  if (!handle || !card || !state.root.contains(card)) {
    return;
  }

  state.drag = {
    card,
    handle,
    pointerId: event.pointerId,
    taskId: card.dataset.taskId,
    taskType: card.dataset.taskType,
    targetCard: null,
    insertAfter: false,
    startX: event.clientX,
    startY: event.clientY,
    active: false
  };
  handle.setPointerCapture?.(event.pointerId);
}

function handlePointerMove(state, event) {
  const drag = state.drag;
  if (!drag || drag.pointerId !== event.pointerId) {
    return;
  }

  const moved = Math.abs(event.clientX - drag.startX) + Math.abs(event.clientY - drag.startY);
  if (!drag.active && moved < 8) {
    return;
  }

  event.preventDefault();
  drag.active = true;
  drag.card.classList.add("task-card-dragging");
  updateAutoScroll(event.clientY);

  const targetCard = findDropTarget(state.root, drag, event.clientX, event.clientY);
  clearDropTarget(state);
  if (!targetCard) {
    return;
  }

  const rect = targetCard.getBoundingClientRect();
  const verticalDistance = Math.abs(event.clientY - (rect.top + rect.height / 2));
  const horizontalDistance = Math.abs(event.clientX - (rect.left + rect.width / 2));
  const useVerticalPlacement = verticalDistance >= horizontalDistance;
  drag.insertAfter = useVerticalPlacement
    ? event.clientY > rect.top + rect.height / 2
    : event.clientX > rect.left + rect.width / 2;
  drag.targetCard = targetCard;
  targetCard.classList.add(drag.insertAfter ? "task-card-drop-after" : "task-card-drop-before");
}

async function finishPointerDrag(state, event) {
  const drag = state.drag;
  if (!drag || drag.pointerId !== event.pointerId) {
    return;
  }

  drag.handle.releasePointerCapture?.(event.pointerId);
  if (drag.active && drag.targetCard) {
    await state.dotNetReference.invokeMethodAsync(
      "HandleTaskDropped",
      drag.taskType,
      drag.taskId,
      drag.targetCard.dataset.taskId,
      drag.insertAfter);
  }

  clearPointerDrag(state);
}

function clearPointerDrag(state) {
  if (state.drag?.card) {
    state.drag.card.classList.remove("task-card-dragging");
  }

  clearDropTarget(state);
  state.drag = null;
}

function clearDropTarget(state) {
  const targetCard = state.drag?.targetCard;
  if (!targetCard) {
    return;
  }

  targetCard.classList.remove("task-card-drop-before", "task-card-drop-after");
  state.drag.targetCard = null;
}

function findDropTarget(root, drag, clientX, clientY) {
  const element = document.elementFromPoint(clientX, clientY);
  const card = element?.closest?.("[data-task-id][data-task-type]");
  if (!card
      || card === drag.card
      || !root.contains(card)
      || card.dataset.taskType !== drag.taskType) {
    return null;
  }

  return card;
}

function updateAutoScroll(clientY) {
  const edgeSize = 72;
  const maxStep = 18;
  if (clientY < edgeSize) {
    window.scrollBy(0, -maxStep);
  } else if (clientY > window.innerHeight - edgeSize) {
    window.scrollBy(0, maxStep);
  }
}
