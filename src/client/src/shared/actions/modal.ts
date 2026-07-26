/**
 * Makes an element behave as a modal dialog for keyboard and assistive-technology users.
 *
 * The overlays in this app already paint over the screen and swallow clicks, so a mouse user
 * cannot reach what is behind them. Keyboard and screen-reader users could: with the settings
 * panel open, 45 controls behind it were still in the tab order, so Tab walked straight out of
 * the dialog into a town screen the user could not see responding. Focus also stayed on whatever
 * opened the dialog rather than moving into it.
 *
 * Rather than a JS focus trap that has to intercept every Tab, this marks everything outside the
 * dialog's ancestor chain `inert` — the platform primitive for exactly this. Inert subtrees are
 * removed from the tab order, from hit testing, and from the accessibility tree, so there is one
 * mechanism instead of three and nothing to keep in sync.
 *
 * Usage: `<div class="my-overlay" role="dialog" aria-modal="true" aria-label="..." use:modal>`
 */
export function modal(node: HTMLElement) {
  const inerted: HTMLElement[] = [];

  /** Inert every sibling along the path from the dialog up to <body>, leaving the dialog reachable. */
  function isolate() {
    let current: HTMLElement | null = node;
    while (current && current !== document.body) {
      const parent: HTMLElement | null = current.parentElement;
      if (!parent) break;
      for (const sibling of Array.from(parent.children)) {
        if (sibling === current) continue;
        if (!(sibling instanceof HTMLElement)) continue;
        if (sibling.inert) continue; // already inert for its own reasons — leave it be
        sibling.inert = true;
        inerted.push(sibling);
      }
      current = parent;
    }
  }

  function release() {
    for (const el of inerted) el.inert = false;
    inerted.length = 0;
  }

  /**
   * Moves focus inside. Prefers the first control so keyboard users land somewhere actionable;
   * falls back to the dialog itself, which is why callers give it tabindex="-1".
   */
  function focusInside() {
    const target = node.querySelector<HTMLElement>(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );
    (target ?? node).focus({ preventScroll: true });
  }

  const previouslyFocused = document.activeElement as HTMLElement | null;

  isolate();
  focusInside();

  return {
    destroy() {
      release();
      // Return focus to whatever opened the dialog, so closing does not dump the user at the top
      // of the document. isConnected guards the case where that element has since been removed.
      if (previouslyFocused?.isConnected) {
        previouslyFocused.focus({ preventScroll: true });
      }
    },
  };
}
