const fieldStates = new WeakMap();

export function initialize(root, dotNetReference) {
  dispose(root);

  const dialog = root.closest("dialog");
  const state = {
    previewUrl: null,
    dialog,
    dotNetReference,
    onDialogClose: null
  };

  state.onDialogClose = () => {
    clear(root);
    state.dotNetReference.invokeMethodAsync("HandleDialogClosedAsync").catch(() => {
      // The interactive circuit may already be disconnected.
    });
  };

  if (dialog) {
    dialog.addEventListener("close", state.onDialogClose);
  }

  fieldStates.set(root, state);
}

export function openPicker(root) {
  root.querySelector('input[type="file"]')?.click();
}

export function replacePreview(root) {
  const state = fieldStates.get(root);
  const input = root.querySelector('input[type="file"]');
  const file = input?.files?.[0];

  releasePreview(state);
  if (!state || !file) {
    return null;
  }

  state.previewUrl = URL.createObjectURL(file);
  return state.previewUrl;
}

export function clear(root) {
  const state = fieldStates.get(root);
  releasePreview(state);

  const input = root.querySelector('input[type="file"]');
  if (input) {
    input.value = "";
  }
}

export function dispose(root) {
  const state = fieldStates.get(root);
  if (!state) {
    return;
  }

  releasePreview(state);
  if (state.dialog && state.onDialogClose) {
    state.dialog.removeEventListener("close", state.onDialogClose);
  }

  fieldStates.delete(root);
}

function releasePreview(state) {
  if (!state?.previewUrl) {
    return;
  }

  URL.revokeObjectURL(state.previewUrl);
  state.previewUrl = null;
}
