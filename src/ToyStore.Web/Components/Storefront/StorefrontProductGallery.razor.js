const swipeStates = new WeakMap();
const minimumSwipeDistance = 40;

export function initialize(viewport, dotNetReference) {
  if (!(viewport instanceof HTMLElement)) return;
  dispose(viewport);

  const state = {
    dotNetReference,
    pointerId: null,
    startX: 0,
    startY: 0,
    listeners: []
  };

  const listen = (type, handler, options) => {
    viewport.addEventListener(type, handler, options);
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

    if (Math.abs(distanceX) < minimumSwipeDistance
      || Math.abs(distanceX) <= Math.abs(distanceY) * 1.2) return;

    const method = distanceX < 0
      ? "ShowNextImageFromSwipeAsync"
      : "ShowPreviousImageFromSwipeAsync";
    void state.dotNetReference.invokeMethodAsync(method).catch(() => {});
  }, { passive: true });

  const cancel = event => {
    if (state.pointerId === event.pointerId) state.pointerId = null;
  };
  listen("pointercancel", cancel, { passive: true });

  swipeStates.set(viewport, state);
}

export function dispose(viewport) {
  if (!(viewport instanceof HTMLElement)) return;
  const state = swipeStates.get(viewport);
  if (!state) return;
  state.listeners.forEach(([type, handler, options]) =>
    viewport.removeEventListener(type, handler, options));
  swipeStates.delete(viewport);
}
