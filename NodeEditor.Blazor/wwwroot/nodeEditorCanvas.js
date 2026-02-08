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

export function downloadFile(fileName, contentType, base64Content) {
  const byteCharacters = atob(base64Content);
  const byteNumbers = new Array(byteCharacters.length);
  for (let i = 0; i < byteCharacters.length; i++) {
    byteNumbers[i] = byteCharacters.charCodeAt(i);
  }
  const byteArray = new Uint8Array(byteNumbers);
  const blob = new Blob([byteArray], { type: contentType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

// ── Local graph storage (localStorage) ──

const STORAGE_PREFIX = 'nodeEditor_savedGraph_';

export function saveGraphToLocal(name, json) {
  localStorage.setItem(STORAGE_PREFIX + name, json);
}

export function loadGraphFromLocal(name) {
  return localStorage.getItem(STORAGE_PREFIX + name);
}

export function deleteGraphFromLocal(name) {
  localStorage.removeItem(STORAGE_PREFIX + name);
}

export function listSavedGraphNames() {
  const names = [];
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (key && key.startsWith(STORAGE_PREFIX)) {
      names.push(key.substring(STORAGE_PREFIX.length));
    }
  }
  names.sort();
  return names;
}

export function promptForName(message, defaultValue) {
  return prompt(message, defaultValue);
}