/** @type {import('tailwindcss').Config} */
export default {
  darkMode: ['class'],
  content: [
    './index.html',
    './src/**/*.{js,ts,jsx,tsx}',
  ],
  theme: {
    container: {
      center: true,
      padding: '2rem',
      screens: {
        '2xl': '1400px',
      },
    },
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'BlinkMacSystemFont', 'sans-serif'],
      },
      colors: {
        border: 'hsl(var(--border))',
        input: 'hsl(var(--input))',
        ring: 'hsl(var(--ring))',
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        primary: {
          DEFAULT: 'hsl(var(--primary))',
          foreground: 'hsl(var(--primary-foreground))',
        },
        secondary: {
          DEFAULT: 'hsl(var(--secondary))',
          foreground: 'hsl(var(--secondary-foreground))',
        },
        destructive: {
          DEFAULT: 'hsl(var(--destructive))',
          foreground: 'hsl(var(--destructive-foreground))',
        },
        muted: {
          DEFAULT: 'hsl(var(--muted))',
          foreground: 'hsl(var(--muted-foreground))',
        },
        accent: {
          DEFAULT: 'hsl(var(--accent))',
          foreground: 'hsl(var(--accent-foreground))',
        },
        popover: {
          DEFAULT: 'hsl(var(--popover))',
          foreground: 'hsl(var(--popover-foreground))',
        },
        card: {
          DEFAULT: 'hsl(var(--card))',
          foreground: 'hsl(var(--card-foreground))',
        },
        surface: {
          canvas: 'rgb(var(--surface-canvas) / <alpha-value>)',
          sidebar: 'rgb(var(--surface-sidebar) / <alpha-value>)',
          card: 'rgb(var(--surface-card) / <alpha-value>)',
          elevated: 'rgb(var(--surface-elevated) / <alpha-value>)',
          subtle: 'rgb(var(--surface-subtle) / <alpha-value>)',
          muted: 'rgb(var(--surface-muted) / <alpha-value>)',
          hover: 'rgb(var(--surface-hover) / <alpha-value>)',
          strong: 'rgb(var(--surface-strong) / <alpha-value>)',
        },
        ink: {
          primary: 'rgb(var(--ink-primary) / <alpha-value>)',
          heading: 'rgb(var(--ink-heading) / <alpha-value>)',
          secondary: 'rgb(var(--ink-secondary) / <alpha-value>)',
          body: 'rgb(var(--ink-body) / <alpha-value>)',
          muted: 'rgb(var(--ink-muted) / <alpha-value>)',
          subtle: 'rgb(var(--ink-subtle) / <alpha-value>)',
          faint: 'rgb(var(--ink-faint) / <alpha-value>)',
        },
        line: {
          subtle: 'rgb(var(--line-subtle) / <alpha-value>)',
          DEFAULT: 'rgb(var(--line-default) / <alpha-value>)',
          input: 'rgb(var(--line-input) / <alpha-value>)',
          strong: 'rgb(var(--line-strong) / <alpha-value>)',
        },
        brand: {
          ink: 'rgb(var(--brand-ink) / <alpha-value>)',
          solid: 'rgb(var(--brand-solid) / <alpha-value>)',
          'solid-hover': 'rgb(var(--brand-solid-hover) / <alpha-value>)',
          'soft-bg': 'rgb(var(--brand-soft-bg) / <alpha-value>)',
          'soft-border': 'rgb(var(--brand-soft-border) / <alpha-value>)',
          'soft-text': 'rgb(var(--brand-soft-text) / <alpha-value>)',
        },
        tone: {
          info: {
            bg: 'rgb(var(--tone-info-bg) / <alpha-value>)',
            border: 'rgb(var(--tone-info-border) / <alpha-value>)',
            text: 'rgb(var(--tone-info-text) / <alpha-value>)',
            icon: 'rgb(var(--tone-info-icon) / <alpha-value>)',
            strong: 'rgb(var(--tone-info-strong) / <alpha-value>)',
          },
          success: {
            bg: 'rgb(var(--tone-success-bg) / <alpha-value>)',
            border: 'rgb(var(--tone-success-border) / <alpha-value>)',
            text: 'rgb(var(--tone-success-text) / <alpha-value>)',
            icon: 'rgb(var(--tone-success-icon) / <alpha-value>)',
            strong: 'rgb(var(--tone-success-strong) / <alpha-value>)',
          },
          warning: {
            bg: 'rgb(var(--tone-warning-bg) / <alpha-value>)',
            border: 'rgb(var(--tone-warning-border) / <alpha-value>)',
            text: 'rgb(var(--tone-warning-text) / <alpha-value>)',
            icon: 'rgb(var(--tone-warning-icon) / <alpha-value>)',
            strong: 'rgb(var(--tone-warning-strong) / <alpha-value>)',
          },
          danger: {
            bg: 'rgb(var(--tone-danger-bg) / <alpha-value>)',
            border: 'rgb(var(--tone-danger-border) / <alpha-value>)',
            text: 'rgb(var(--tone-danger-text) / <alpha-value>)',
            icon: 'rgb(var(--tone-danger-icon) / <alpha-value>)',
            strong: 'rgb(var(--tone-danger-strong) / <alpha-value>)',
          },
          violet: {
            bg: 'rgb(var(--tone-violet-bg) / <alpha-value>)',
            border: 'rgb(var(--tone-violet-border) / <alpha-value>)',
            text: 'rgb(var(--tone-violet-text) / <alpha-value>)',
            icon: 'rgb(var(--tone-violet-icon) / <alpha-value>)',
          },
          orange: {
            bg: 'rgb(var(--tone-orange-bg) / <alpha-value>)',
            border: 'rgb(var(--tone-orange-border) / <alpha-value>)',
            text: 'rgb(var(--tone-orange-text) / <alpha-value>)',
            icon: 'rgb(var(--tone-orange-icon) / <alpha-value>)',
          },
        },
      },
      borderRadius: {
        lg: 'var(--radius)',
        md: 'calc(var(--radius) - 2px)',
        sm: 'calc(var(--radius) - 4px)',
      },
      keyframes: {
        'accordion-down': {
          from: { height: '0' },
          to: { height: 'var(--radix-accordion-content-height)' },
        },
        'accordion-up': {
          from: { height: 'var(--radix-accordion-content-height)' },
          to: { height: '0' },
        },
      },
      animation: {
        'accordion-down': 'accordion-down 0.2s ease-out',
        'accordion-up': 'accordion-up 0.2s ease-out',
      },
    },
  },
  plugins: [require('tailwindcss-animate')],
};
