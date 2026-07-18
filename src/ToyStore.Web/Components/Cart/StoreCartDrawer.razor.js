const storageKey = "sytoy:anonymous-cart:v1";
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function isSnapshot(value) {
  return value !== null
    && typeof value === "object"
    && guidPattern.test(value.mergeOperationId)
    && Array.isArray(value.items)
    && value.items.length <= 100
    && value.items.every(item => item !== null
      && typeof item === "object"
      && guidPattern.test(item.productId)
      && Number.isInteger(item.quantity)
      && item.quantity >= 1
      && item.quantity <= 99);
}

export function load() {
  try {
    const value = window.localStorage.getItem(storageKey);
    if (!value) return null;
    const parsed = JSON.parse(value);
    if (!isSnapshot(parsed)) throw new TypeError("Invalid anonymous cart storage");
    return parsed;
  } catch {
    window.localStorage.removeItem(storageKey);
    return null;
  }
}

export function save(snapshot) {
  window.localStorage.setItem(storageKey, JSON.stringify({
    mergeOperationId: snapshot.mergeOperationId,
    items: snapshot.items.map(item => ({
      productId: item.productId,
      quantity: item.quantity
    }))
  }));
}

export function clear() {
  window.localStorage.removeItem(storageKey);
}
