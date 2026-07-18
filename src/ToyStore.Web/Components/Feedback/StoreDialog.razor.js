const dialogStates = new WeakMap();
const drawerAnimationDurationMs = 300;
const drawerAnimationEasing = "cubic-bezier(.2,.8,.2,1)";

function isDialog(dialog) {
  return dialog instanceof HTMLDialogElement;
}

function prefersReducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function drawerOffCanvasTransform(dialog) {
  return dialog.classList.contains("store-drawer--start")
    ? "translateX(-100%)"
    : "translateX(100%)";
}

async function animateDrawer(dialog, opening) {
  if (!dialog.classList.contains("store-drawer") || prefersReducedMotion()) return;
  const surface = dialog.querySelector(".store-dialog__surface");
  if (!(surface instanceof HTMLElement)) return;

  surface.getAnimations().forEach(animation => animation.cancel());
  const offCanvas = drawerOffCanvasTransform(dialog);
  const keyframes = opening
    ? [{ transform: offCanvas }, { transform: "translateX(0)" }]
    : [{ transform: "translateX(0)" }, { transform: offCanvas }];
  const animation = surface.animate(keyframes, {
    duration: drawerAnimationDurationMs,
    easing: drawerAnimationEasing,
    fill: "both"
  });

  try {
    await animation.finished;
  } catch {
    // A rapid open/close can intentionally replace the current animation.
  } finally {
    animation.cancel();
  }
}

async function closeDrawer(dialog) {
  if (!dialog.open) return;
  if (!dialog.classList.contains("store-drawer") || prefersReducedMotion()) {
    dialog.close();
    return;
  }

  const surface = dialog.querySelector(".store-dialog__surface");
  if (!(surface instanceof HTMLElement)) {
    dialog.close();
    return;
  }

  await animateDrawer(dialog, false);
  if (dialog.open) dialog.close();
}

function preventBusyDialogCancel(event) {
  const dialog = event.target;
  if (dialog instanceof HTMLDialogElement && dialog.dataset.dismissible === "false") {
    event.preventDefault();
  }
}

function preventBusyDialogEscape(event) {
  if (event.key === "Escape" && document.querySelector('dialog[open][data-dismissible="false"]')) {
    event.preventDefault();
  }
}

document.addEventListener("cancel", preventBusyDialogCancel, true);
document.addEventListener("keydown", preventBusyDialogEscape, true);

export function initialize(dialog, dotNetReference) {
  if (!isDialog(dialog)) return;
  dispose(dialog);

  const state = {
    dotNetReference,
    returnFocusElement: null,
    restoreFocusOnClose: true,
    dismissible: true,
    isClosing: false,
    onCancel: null,
    onClose: null,
    onPointerDown: null
  };

  state.onCancel = event => {
    if (dialog.dataset.dismissible === "false" || !state.dismissible) {
      event.preventDefault();
    }
  };

  state.onClose = () => {
    state.isClosing = false;
    const returnFocusElement = state.returnFocusElement;
    state.returnFocusElement = null;

    if (state.restoreFocusOnClose && returnFocusElement instanceof HTMLElement && returnFocusElement.isConnected) {
      returnFocusElement.focus();
    }

    state.restoreFocusOnClose = true;

    state.dotNetReference.invokeMethodAsync("HandleNativeClosedAsync").catch(() => {
      // The circuit may already be disconnected or disposed.
    });
  };

  state.onPointerDown = async event => {
    if (event.target !== dialog || state.isClosing
      || dialog.dataset.dismissible === "false" || !state.dismissible) return;

    event.preventDefault();
    state.isClosing = true;
    try {
      await closeDrawer(dialog);
    } finally {
      state.isClosing = false;
    }
  };

  dialog.addEventListener("cancel", state.onCancel);
  dialog.addEventListener("close", state.onClose);
  dialog.addEventListener("pointerdown", state.onPointerDown);
  dialogStates.set(dialog, state);
}

export function setDismissible(dialog, dismissible) {
  if (!isDialog(dialog)) return;
  const state = dialogStates.get(dialog);
  if (state) {
    state.dismissible = dismissible;
  }
}

export async function show(dialog, initialFocusSelector) {
  if (!isDialog(dialog)) return;
  const state = dialogStates.get(dialog);

  if (!dialog.open) {
    if (state) {
      state.returnFocusElement = document.activeElement;
      state.restoreFocusOnClose = true;
    }

    dialog.showModal();

    if (initialFocusSelector) {
      dialog.querySelector(initialFocusSelector)?.focus();
    }

    // Native dialog top-layer rendering can skip CSS starting styles. A direct compositor
    // animation guarantees the drawer visibly travels from the viewport edge.
    await animateDrawer(dialog, true);
  }
}

export async function close(dialog) {
  if (!isDialog(dialog)) return;
  const state = dialogStates.get(dialog);
  if (dialog.open) {
    if (state) {
      state.restoreFocusOnClose = true;
    }

    await closeDrawer(dialog);
  }
}

export async function closeWithoutFocusReturn(dialog) {
  if (!isDialog(dialog)) return;
  const state = dialogStates.get(dialog);
  if (state) {
    state.restoreFocusOnClose = false;
  }

  await closeDrawer(dialog);
}

export function dispose(dialog) {
  if (!isDialog(dialog)) return;
  const state = dialogStates.get(dialog);
  if (!state) {
    return;
  }

  dialog.removeEventListener("close", state.onClose);
  dialog.removeEventListener("cancel", state.onCancel);
  dialog.removeEventListener("pointerdown", state.onPointerDown);

  if (dialog.open) {
    dialog.close();
  }

  if (state.restoreFocusOnClose && state.returnFocusElement instanceof HTMLElement && state.returnFocusElement.isConnected) {
    state.returnFocusElement.focus();
  }

  dialogStates.delete(dialog);
}
