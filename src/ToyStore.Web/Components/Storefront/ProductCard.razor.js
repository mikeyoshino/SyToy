const swipeStates = new WeakMap();
const minimumSwipeDistance = 40;

export function initialize(media, dotNetReference) {
  if (!(media instanceof HTMLElement)) return;
  dispose(media);

  const state = {
    dotNetReference,
    pointerId: null,
    startX: 0,
    startY: 0,
    suppressClick: false,
    suppressTimer: 0,
    listeners: []
  };

  const listen = (type, handler, options) => {
    media.addEventListener(type, handler, options);
    state.listeners.push([type, handler, options]);
  };

  listen("pointerdown", event => {
    if (!event.isPrimary || (event.pointerType !== "touch" && event.pointerType !== "pen")) return;
    state.pointerId = event.pointerId;
    state.startX = event.clientX;
    state.startY = event.clientY;
  }, { passive: true });

  listen("pointerup", event => {
    if (state.pointerId !== event.pointerId) return;
    const distanceX = event.clientX - state.startX;
    const distanceY = event.clientY - state.startY;
    state.pointerId = null;

    if (Math.abs(distanceX) < minimumSwipeDistance || Math.abs(distanceX) <= Math.abs(distanceY) * 1.2) return;

    state.suppressClick = true;
    if (state.suppressTimer) window.clearTimeout(state.suppressTimer);
    state.suppressTimer = window.setTimeout(() => {
      state.suppressClick = false;
      state.suppressTimer = 0;
    }, 500);

    const method = distanceX < 0
      ? "ShowNextImageFromSwipeAsync"
      : "ShowPreviousImageFromSwipeAsync";
    void state.dotNetReference.invokeMethodAsync(method).catch(() => {});
  }, { passive: true });

  const cancel = event => {
    if (state.pointerId === event.pointerId) state.pointerId = null;
  };
  listen("pointercancel", cancel, { passive: true });

  listen("click", event => {
    if (!state.suppressClick) return;
    state.suppressClick = false;
    if (state.suppressTimer) window.clearTimeout(state.suppressTimer);
    state.suppressTimer = 0;
    event.preventDefault();
    event.stopImmediatePropagation();
  }, true);

  swipeStates.set(media, state);
}

export function dispose(media) {
  if (!(media instanceof HTMLElement)) return;
  const state = swipeStates.get(media);
  if (!state) return;
  state.listeners.forEach(([type, handler, options]) => media.removeEventListener(type, handler, options));
  if (state.suppressTimer) window.clearTimeout(state.suppressTimer);
  swipeStates.delete(media);
}
