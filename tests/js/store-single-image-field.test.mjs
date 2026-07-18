import assertModule from "assert";
import fsModule from "fs";

const assert = assertModule.strict;
const fs = fsModule.promises;

const source = await fs.readFile(
  new URL("../../src/ToyStore.Web/Components/Forms/StoreSingleImageField.razor.js", import.meta.url),
  "utf8");
const module = await import(`data:text/javascript,${encodeURIComponent(source)}`);

const created = [];
const revoked = [];
URL.createObjectURL = file => {
  const value = `blob:test-${created.length + 1}`;
  created.push({ file, value });
  return value;
};
URL.revokeObjectURL = value => revoked.push(value);

const listeners = new Map();
const dialog = {
  addEventListener(name, callback) {
    listeners.set(name, callback);
  },
  removeEventListener(name, callback) {
    if (listeners.get(name) === callback) {
      listeners.delete(name);
    }
  }
};
const input = {
  files: [{ name: "first.webp" }],
  value: "selected",
  clickCount: 0,
  click() {
    this.clickCount++;
  }
};
const root = {
  closest(selector) {
    assert.equal(selector, "dialog");
    return dialog;
  },
  querySelector(selector) {
    assert.equal(selector, 'input[type="file"]');
    return input;
  }
};
const invoked = [];
const dotNetReference = {
  invokeMethodAsync(name) {
    invoked.push(name);
    return Promise.resolve();
  }
};

module.initialize(root, dotNetReference);
module.openPicker(root);
assert.equal(input.clickCount, 1);

assert.equal(module.replacePreview(root), "blob:test-1");
input.files = [{ name: "replacement.png" }];
assert.equal(module.replacePreview(root), "blob:test-2");
assert.deepEqual(revoked, ["blob:test-1"]);

module.clear(root);
assert.equal(input.value, "");
assert.deepEqual(revoked, ["blob:test-1", "blob:test-2"]);

input.files = [{ name: "dialog-close.jpg" }];
assert.equal(module.replacePreview(root), "blob:test-3");
listeners.get("close")();
await Promise.resolve();
assert.deepEqual(revoked, ["blob:test-1", "blob:test-2", "blob:test-3"]);
assert.deepEqual(invoked, ["HandleDialogClosedAsync"]);

input.files = [{ name: "dispose.webp" }];
assert.equal(module.replacePreview(root), "blob:test-4");
module.dispose(root);
assert.deepEqual(revoked, ["blob:test-1", "blob:test-2", "blob:test-3", "blob:test-4"]);
assert.equal(listeners.has("close"), false);

console.log("StoreSingleImageField blob lifecycle: passed");
