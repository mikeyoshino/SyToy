const previews = new WeakMap();

export function openPicker(root) {
  root.querySelector('input[type="file"]')?.click();
}

export function createPreviews(root) {
  release(root);
  const files = Array.from(root.querySelector('input[type="file"]')?.files ?? []);
  const urls = files.map(file => URL.createObjectURL(file));
  previews.set(root, urls);
  return urls;
}

export function dispose(root) {
  release(root);
}

export function clear(root) {
  release(root);
  const input = root.querySelector('input[type="file"]');
  if (input) input.value = "";
}

function release(root) {
  for (const url of previews.get(root) ?? []) URL.revokeObjectURL(url);
  previews.delete(root);
}
