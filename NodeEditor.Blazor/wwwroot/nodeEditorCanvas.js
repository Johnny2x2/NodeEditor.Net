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

export function getCanvasScreenOffset(element) {
  if (!element) {
    return { x: 0, y: 0 };
  }

  const rect = element.getBoundingClientRect();
  return { x: rect.left, y: rect.top };
}

export function getSocketDotPositions(element) {
  if (!element) {
    return [];
  }

  const canvasRect = element.getBoundingClientRect();
  const sockets = element.querySelectorAll('.ne-socket');
  const results = [];

  sockets.forEach(socket => {
    const dot = socket.querySelector('.ne-socket-dot');
    if (!dot) {
      return;
    }

    const dotRect = dot.getBoundingClientRect();
    const centerX = dotRect.left + dotRect.width / 2 - canvasRect.left;
    const centerY = dotRect.top + dotRect.height / 2 - canvasRect.top;

    results.push({
      nodeId: socket.dataset.nodeId || '',
      socketName: socket.dataset.socketName || '',
      isInput: (socket.dataset.socketIsInput || '').toLowerCase() === 'true',
      x: centerX,
      y: centerY
    });
  });

  return results;
}