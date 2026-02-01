const observers = new WeakMap();

export function observeCanvasSize(element, dotNetRef) {
  if (!element || !dotNetRef) {
    return;
  }

  const observer = new ResizeObserver(entries => {
    for (const entry of entries) {
      const rect = entry.contentRect;
      dotNetRef.invokeMethodAsync('OnCanvasResize', rect.width, rect.height);
    }
  });

  observer.observe(element);
  observers.set(element, observer);
}

export function disconnectCanvasObserver(element) {
  const observer = observers.get(element);
  if (observer) {
    observer.disconnect();
    observers.delete(element);
  }
}