namespace ToyStore.Web.Components.Forms;

public sealed record SelectOption<TValue>(TValue Value, string Label, bool Disabled = false);
