const toggleSelector = "[data-password-toggle]";

document.addEventListener("click", event => {
  const toggle = event.target instanceof Element
    ? event.target.closest(toggleSelector)
    : null;
  if (!(toggle instanceof HTMLButtonElement) || toggle.disabled) return;

  const inputId = toggle.dataset.passwordInput;
  const input = inputId ? document.getElementById(inputId) : null;
  if (!(input instanceof HTMLInputElement)) return;

  const reveal = input.type === "password";
  input.type = reveal ? "text" : "password";
  toggle.setAttribute("aria-pressed", reveal ? "true" : "false");
  toggle.setAttribute("aria-label", reveal ? "ซ่อนรหัสผ่าน" : "แสดงรหัสผ่าน");
  input.focus({ preventScroll: true });
});
