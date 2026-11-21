import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { flushSync } from 'react-dom';

export type ThemeMode = 'light' | 'dark';

export interface ThemeTransitionOrigin {
  x: number;
  y: number;
}

export interface ThemeTransitionOptions {
  origin?: ThemeTransitionOrigin | null;
  sourceElement?: HTMLElement | null;
  onBeforeCommit?: () => void;
}

const THEME_STORAGE_KEY = 'ugtm:theme';
const THEME_TRANSITION_CLASS = 'theme-transitioning';
const REVEAL_DURATION_MS = 480;
const REVEAL_EASING = 'cubic-bezier(0.4, 0, 0.2, 1)';

interface ViewTransitionLike {
  ready: Promise<void>;
  finished: Promise<void>;
}

type DocumentWithViewTransition = Document & {
  startViewTransition?: (callback: () => void) => ViewTransitionLike;
};

interface ThemeContextValue {
  theme: ThemeMode;
  resolvedTheme: ThemeMode;
  setTheme: (next: ThemeMode, options?: ThemeTransitionOptions) => void;
  toggleTheme: (options?: ThemeTransitionOptions) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function isThemeMode(value: unknown): value is ThemeMode {
  return value === 'light' || value === 'dark';
}

function readStoredTheme(): ThemeMode {
  if (typeof window === 'undefined') return 'light';
  try {
    const raw = window.localStorage.getItem(THEME_STORAGE_KEY);
    return isThemeMode(raw) ? raw : 'light';
  } catch {
    return 'light';
  }
}

function applyDocumentTheme(mode: ThemeMode) {
  if (typeof document === 'undefined') return;
  const root = document.documentElement;
  root.classList.toggle('dark', mode === 'dark');
  root.dataset.theme = mode;
  root.style.colorScheme = mode;
}

function persistTheme(mode: ThemeMode) {
  try {
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(THEME_STORAGE_KEY, mode);
    }
  } catch {
    // Ignore storage quota or private browsing errors.
  }
}

function resolveTransitionOrigin(options?: ThemeTransitionOptions): ThemeTransitionOrigin | null {
  if (options?.origin) {
    return options.origin;
  }
  const element = options?.sourceElement;
  if (element && typeof element.getBoundingClientRect === 'function') {
    const rect = element.getBoundingClientRect();
    if (rect.width > 0 || rect.height > 0) {
      return {
        x: rect.left + rect.width / 2,
        y: rect.top + rect.height / 2,
      };
    }
  }
  return null;
}

function canAnimateThemeTransition(): boolean {
  if (typeof document === 'undefined' || typeof window === 'undefined') return false;
  const doc = document as DocumentWithViewTransition;
  if (typeof doc.startViewTransition !== 'function') return false;
  if (
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches
  ) {
    return false;
  }
  return true;
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<ThemeMode>(() => readStoredTheme());
  const themeRef = useRef<ThemeMode>(theme);
  const transitionIdRef = useRef(0);

  useEffect(() => {
    themeRef.current = theme;
    applyDocumentTheme(theme);
  }, [theme]);

  useEffect(() => {
    return () => {
      if (typeof document !== 'undefined') {
        document.documentElement.classList.remove(THEME_TRANSITION_CLASS);
      }
    };
  }, []);

  useEffect(() => {
    if (typeof window === 'undefined') return;
    const handleStorage = (event: StorageEvent) => {
      if (event.key !== THEME_STORAGE_KEY) return;
      const next: ThemeMode = event.newValue === 'dark' ? 'dark' : 'light';
      themeRef.current = next;
      setThemeState(next);
    };
    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
  }, []);

  const setTheme = useCallback((next: ThemeMode, options?: ThemeTransitionOptions) => {
    if (next === themeRef.current) {
      options?.onBeforeCommit?.();
      return;
    }

    const commitImmediate = () => {
      themeRef.current = next;
      applyDocumentTheme(next);
      persistTheme(next);
      setThemeState(next);
      options?.onBeforeCommit?.();
    };

    const origin = resolveTransitionOrigin(options);
    if (!origin || !canAnimateThemeTransition()) {
      commitImmediate();
      return;
    }

    const doc = document as DocumentWithViewTransition;
    const root = document.documentElement;
    const transitionId = ++transitionIdRef.current;
    const endRadius = Math.hypot(
      Math.max(origin.x, window.innerWidth - origin.x),
      Math.max(origin.y, window.innerHeight - origin.y),
    );

    try {
      const transition = doc.startViewTransition!(() => {
        root.classList.add(THEME_TRANSITION_CLASS);
        flushSync(() => {
          commitImmediate();
        });
      });

      transition.ready
        .then(() => {
          if (typeof root.animate !== 'function') return;
          root.animate(
            {
              clipPath: [
                `circle(0px at ${origin.x}px ${origin.y}px)`,
                `circle(${endRadius}px at ${origin.x}px ${origin.y}px)`,
              ],
            },
            {
              duration: REVEAL_DURATION_MS,
              easing: REVEAL_EASING,
              pseudoElement: '::view-transition-new(root)',
            },
          );
        })
        .catch(() => {});

      transition.finished
        .catch(() => {})
        .finally(() => {
          if (transitionIdRef.current === transitionId) {
            root.classList.remove(THEME_TRANSITION_CLASS);
          }
        });
    } catch {
      root.classList.remove(THEME_TRANSITION_CLASS);
      commitImmediate();
    }
  }, []);

  const toggleTheme = useCallback(
    (options?: ThemeTransitionOptions) => {
      const next: ThemeMode = themeRef.current === 'dark' ? 'light' : 'dark';
      setTheme(next, options);
    },
    [setTheme],
  );

  const value = useMemo<ThemeContextValue>(
    () => ({
      theme,
      resolvedTheme: theme,
      setTheme,
      toggleTheme,
    }),
    [theme, setTheme, toggleTheme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

const FALLBACK_THEME: ThemeContextValue = {
  theme: 'light',
  resolvedTheme: 'light',
  setTheme: () => {},
  toggleTheme: () => {},
};

export function useTheme(): ThemeContextValue {
  return useContext(ThemeContext) ?? FALLBACK_THEME;
}
