import assert from "assert";
import { readFile } from "fs/promises";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const modulePath = resolve(
  repositoryRoot,
  "src/ToyStore.Web/Components/Forms/StoreSingleImageField.razor.js");
const source = await readFile(modulePath, "utf8");
const imageField = await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);

const created = [];
const revoked = [];
const originalCreateObjectUrl = URL.createObjectURL;
const originalRevokeObjectUrl = URL.revokeObjectURL;
URL.createObjectURL = file => {
  const value = `blob:test-${created.length + 1}`;
  created.push({ file, value });
  return value;
};
URL.revokeObjectURL = value => revoked.push(value);

try {
  const listeners = new Map();
  const dialog = {
    addEventListener(type, callback) {
      listeners.set(type, callback);
    },
    removeEventListener(type, callback) {
      if (listeners.get(type) === callback) {
        listeners.delete(type);
      }
    }
  };
  const input = { files: [{ name: "brand.webp" }], value: "brand.webp" };
  const root = {
    closest(selector) {
      return selector === "dialog" ? dialog : null;
    },
    querySelector(selector) {
      return selector === 'input[type="file"]' ? input : null;
    }
  };
  let closedCount = 0;
  const dotNetReference = {
    invokeMethodAsync(method) {
      assert.equal(method, "HandleDialogClosedAsync");
      closedCount += 1;
      return Promise.resolve();
    }
  };

  imageField.initialize(root, dotNetReference);
  assert.equal(imageField.replacePreview(root), "blob:test-1");
  assert.equal(imageField.replacePreview(root), "blob:test-2");
  assert.deepEqual(revoked, ["blob:test-1"]);

  imageField.clear(root);
  assert.equal(input.value, "");
  assert.deepEqual(revoked, ["blob:test-1", "blob:test-2"]);

  input.files = [{ name: "replacement.png" }];
  assert.equal(imageField.replacePreview(root), "blob:test-3");
  listeners.get("close")();
  await Promise.resolve();
  assert.equal(closedCount, 1);
  assert.deepEqual(revoked, ["blob:test-1", "blob:test-2", "blob:test-3"]);

  input.files = [{ name: "dispose.jpg" }];
  assert.equal(imageField.replacePreview(root), "blob:test-4");
  imageField.dispose(root);
  assert.deepEqual(revoked, ["blob:test-1", "blob:test-2", "blob:test-3", "blob:test-4"]);
  assert.equal(listeners.has("close"), false);

  for (const prohibited of ["FileReader", "readAsDataURL", "fetch(", "XMLHttpRequest"]) {
    assert.equal(source.includes(prohibited), false, `Unexpected byte-reading/upload API: ${prohibited}`);
  }

  console.log("Admin Task 10 image lifecycle JS verification passed.");
} finally {
  URL.createObjectURL = originalCreateObjectUrl;
  URL.revokeObjectURL = originalRevokeObjectUrl;
}
