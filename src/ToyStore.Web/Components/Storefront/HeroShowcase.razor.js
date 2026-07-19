const states = new WeakMap();

function reducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function setActive(root, state, index, announce = false) {
  const next = Math.max(0, Math.min(index, state.slides.length - 1));
  state.current = next;
  state.slides.forEach((slide, slideIndex) => {
    const inactive = slideIndex !== next;
    slide.inert = inactive;
    slide.setAttribute("aria-hidden", inactive ? "true" : "false");
  });
  state.dots.forEach((dot, dotIndex) => {
    if (dotIndex === next) dot.setAttribute("aria-current", "true");
    else dot.removeAttribute("aria-current");
  });
  root.dataset.autoplayRunning = "false";
  requestAnimationFrame(() => {
    root.dataset.autoplayRunning = canAutoplay(state) ? "true" : "false";
  });

  if (announce) {
    const slide = state.slides[next];
    slide.setAttribute("aria-live", "polite");
    window.setTimeout(() => slide.removeAttribute("aria-live"), 500);
  }
}

function canAutoplay(state) {
  return state.slides.length > 1
    && !state.userPaused
    && !state.interactionPaused
    && !state.documentHidden
    && state.isVisible
    && !reducedMotion();
}

function clearTimer(root, state) {
  if (state.timerId) window.clearTimeout(state.timerId);
  state.timerId = 0;
  root.dataset.autoplayRunning = "false";
}

function schedule(root, state) {
  clearTimer(root, state);
  if (!canAutoplay(state)) return;
  root.dataset.autoplayRunning = "true";
  state.timerId = window.setTimeout(() => {
    goTo(root, state, (state.current + 1) % state.slides.length, false);
  }, state.intervalMs);
}

function goTo(root, state, index, announce) {
  const next = (index + state.slides.length) % state.slides.length;
  setActive(root, state, next, announce);
  state.track.scrollTo({
    left: state.slides[next].offsetLeft,
    behavior: reducedMotion() ? "auto" : "smooth"
  });
  schedule(root, state);
}

function syncFromScroll(root, state) {
  if (state.scrollFrame) cancelAnimationFrame(state.scrollFrame);
  state.scrollFrame = requestAnimationFrame(() => {
    state.scrollFrame = 0;
    let closest = 0;
    let distance = Number.POSITIVE_INFINITY;
    state.slides.forEach((slide, index) => {
      const nextDistance = Math.abs(slide.offsetLeft - state.track.scrollLeft);
      if (nextDistance < distance) {
        distance = nextDistance;
        closest = index;
      }
    });
    if (closest !== state.current) setActive(root, state, closest, false);
  });
}

function setInteractionPaused(root, state, paused) {
  state.interactionPaused = paused;
  if (paused) clearTimer(root, state);
  else schedule(root, state);
}

function updateAutoplayButton(state) {
  if (!(state.autoplayButton instanceof HTMLButtonElement)) return;
  state.autoplayButton.setAttribute("aria-pressed", state.userPaused ? "true" : "false");
  state.autoplayButton.setAttribute(
    "aria-label",
    state.userPaused ? "เล่นสไลด์อัตโนมัติ" : "หยุดสไลด์อัตโนมัติ");
  const icon = state.autoplayButton.querySelector("span");
  if (icon) icon.textContent = state.userPaused ? "▶" : "Ⅱ";
}

export function initialize(root, intervalMs = 3000) {
  if (!(root instanceof HTMLElement)) return;
  dispose(root);
  const track = root.querySelector("[data-carousel-track]");
  const slides = [...root.querySelectorAll("[data-carousel-slide]")];
  if (!(track instanceof HTMLElement) || slides.length < 2) return;

  root.style.setProperty("--hero-autoplay-duration", `${intervalMs}ms`);
  const state = {
    track,
    slides,
    dots: [...root.querySelectorAll("[data-carousel-target]")],
    autoplayButton: root.querySelector("[data-carousel-autoplay]"),
    intervalMs,
    current: 0,
    timerId: 0,
    scrollFrame: 0,
    userPaused: false,
    interactionPaused: false,
    documentHidden: document.hidden,
    isVisible: true,
    listeners: [],
    observer: null
  };

  const listen = (target, type, handler, options) => {
    target.addEventListener(type, handler, options);
    state.listeners.push([target, type, handler, options]);
  };
  listen(root.querySelector("[data-carousel-previous]"), "click", () => goTo(root, state, state.current - 1, true));
  listen(root.querySelector("[data-carousel-next]"), "click", () => goTo(root, state, state.current + 1, true));
  state.dots.forEach((dot, index) => listen(dot, "click", () => goTo(root, state, index, true)));
  listen(state.autoplayButton, "click", () => {
    state.userPaused = !state.userPaused;
    updateAutoplayButton(state);
    schedule(root, state);
  });
  listen(track, "scroll", () => syncFromScroll(root, state), { passive: true });
  listen(root, "pointerenter", () => setInteractionPaused(root, state, true));
  listen(root, "pointerleave", () => setInteractionPaused(root, state, false));
  listen(root, "focusin", () => setInteractionPaused(root, state, true));
  listen(root, "focusout", event => {
    if (!root.contains(event.relatedTarget)) setInteractionPaused(root, state, false);
  });
  listen(track, "pointerdown", () => setInteractionPaused(root, state, true));
  listen(track, "pointerup", () => setInteractionPaused(root, state, false));
  listen(track, "pointercancel", () => setInteractionPaused(root, state, false));
  listen(document, "visibilitychange", () => {
    state.documentHidden = document.hidden;
    schedule(root, state);
  });

  state.observer = new IntersectionObserver(entries => {
    state.isVisible = entries[0]?.isIntersecting ?? false;
    schedule(root, state);
  }, { threshold: .25 });
  state.observer.observe(root);
  states.set(root, state);
  setActive(root, state, 0, false);
  updateAutoplayButton(state);
  schedule(root, state);
}

export function dispose(root) {
  if (!(root instanceof HTMLElement)) return;
  const state = states.get(root);
  if (!state) return;
  clearTimer(root, state);
  if (state.scrollFrame) cancelAnimationFrame(state.scrollFrame);
  state.listeners.forEach(([target, type, handler, options]) => {
    target?.removeEventListener(type, handler, options);
  });
  state.observer?.disconnect();
  root.style.removeProperty("--hero-autoplay-duration");
  delete root.dataset.autoplayRunning;
  states.delete(root);
}
