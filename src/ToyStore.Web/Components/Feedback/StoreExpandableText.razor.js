const states = new WeakMap();

function readOverflow(root) {
  if (!(root instanceof HTMLElement)
    || root.classList.contains("store-expandable-text--expanded")) return false;
  const content = root.querySelector(".store-expandable-text__content");
  return content instanceof HTMLElement
    && content.scrollHeight > content.clientHeight + 1;
}

export function measure(root) {
  if (!(root instanceof HTMLElement)) return;
  const state = states.get(root);
  if (!state || root.classList.contains("store-expandable-text--expanded")) return;
  const overflow = readOverflow(root);
  if (state.lastOverflow === overflow) return;
  state.lastOverflow = overflow;
  state.dotNetReference.invokeMethodAsync("SetOverflowAsync", overflow).catch(() => {
    // The Blazor circuit may already be disconnected.
  });
}

export function initialize(root, dotNetReference) {
  if (!(root instanceof HTMLElement)) return;
  dispose(root);
  const state = {
    dotNetReference,
    lastOverflow: null,
    observer: new ResizeObserver(() => measure(root))
  };
  state.observer.observe(root);
  states.set(root, state);
  requestAnimationFrame(() => measure(root));
}

export function dispose(root) {
  if (!(root instanceof HTMLElement)) return;
  const state = states.get(root);
  state?.observer.disconnect();
  states.delete(root);
}
