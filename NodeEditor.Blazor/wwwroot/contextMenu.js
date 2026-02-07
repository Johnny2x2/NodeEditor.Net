export function positionContextMenu(element, x, y, padding = 8) {
  if (!element) {
    return;
  }

  const rect = element.getBoundingClientRect();
  const viewportWidth = window.innerWidth || document.documentElement.clientWidth || 0;
  const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;
  const pad = Number.isFinite(padding) ? padding : 8;

  let left = x;
  let top = y;

  if (left + rect.width + pad > viewportWidth) {
    left = Math.max(pad, viewportWidth - rect.width - pad);
  }

  if (top + rect.height + pad > viewportHeight) {
    top = Math.max(pad, viewportHeight - rect.height - pad);
  }

  if (left < pad) {
    left = pad;
  }

  if (top < pad) {
    top = pad;
  }

  element.style.left = `${left}px`;
  element.style.top = `${top}px`;
}
