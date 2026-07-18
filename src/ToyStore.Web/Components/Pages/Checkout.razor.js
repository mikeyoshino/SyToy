let embeddedCheckout;

async function loadStripe() {
  if (window.Stripe) return;
  await new Promise((resolve, reject) => {
    const existing = document.querySelector('script[data-toystore-stripe]');
    if (existing) {
      if (window.Stripe) resolve();
      else {
        existing.addEventListener('load', resolve, { once: true });
        existing.addEventListener('error', reject, { once: true });
      }
      return;
    }
    const script = document.createElement('script');
    script.src = 'https://js.stripe.com/clover/stripe.js';
    script.async = true;
    script.dataset.toystoreStripe = 'true';
    script.addEventListener('load', resolve, { once: true });
    script.addEventListener('error', reject, { once: true });
    document.head.appendChild(script);
  });
}

export async function mount(selector, publishableKey, clientSecret, dotNetReference) {
  await dispose();
  await loadStripe();
  const stripe = window.Stripe(publishableKey);
  embeddedCheckout = await stripe.initEmbeddedCheckout({
    fetchClientSecret: async () => clientSecret,
    onComplete: () => dotNetReference.invokeMethodAsync('HandleStripeCompletedAsync')
  });
  embeddedCheckout.mount(selector);
}

export async function dispose() {
  if (embeddedCheckout) {
    embeddedCheckout.destroy();
    embeddedCheckout = undefined;
  }
}
