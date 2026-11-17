/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
        mono: ['JetBrains Mono', 'Fira Code', 'monospace'],
      },
      colors: {
        brand: {
          blue: '#2054A5',
          orange: '#FA7921',
          dark: '#10182c',
          subtle: '#f8fafc',
          navy: '#0f172a',
        },
        primary: {
          DEFAULT: '#2054A5',
          hover: '#184285',
          light: '#eff6ff',
        },
      },
      backgroundImage: {
        'gradient-brand': 'linear-gradient(135deg, #2054A5 0%, #FA7921 100%)',
        'gradient-brand-reverse': 'linear-gradient(135deg, #FA7921 0%, #2054A5 100%)',
        'gradient-card': 'linear-gradient(180deg, #ffffff 0%, #f8fafc 100%)',
      },
      boxShadow: {
        'subtle': '0 1px 3px 0 rgba(0, 0, 0, 0.05), 0 1px 2px -1px rgba(0, 0, 0, 0.05)',
        'elevated': '0 10px 25px -5px rgba(32, 84, 165, 0.08), 0 8px 10px -6px rgba(32, 84, 165, 0.04)',
      },
    },
  },
  plugins: [],
}

