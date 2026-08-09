import { useEffect, useRef } from 'react';

/**
 * Invoke `callback` every `seconds`. A falsy/zero `seconds` disables the timer.
 * The latest callback is always used (no stale closure), and the interval is
 * cleared on unmount and reset whenever `seconds` changes.
 */
export default function useAutoRefresh(callback, seconds) {
  const cbRef = useRef(callback);
  cbRef.current = callback;

  useEffect(() => {
    const s = Number(seconds);
    if (!s || s <= 0) return undefined;
    const id = setInterval(() => cbRef.current?.(), s * 1000);
    return () => clearInterval(id);
  }, [seconds]);
}
