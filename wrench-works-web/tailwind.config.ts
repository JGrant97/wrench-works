import type { Config } from "tailwindcss";

function surfaceColor(shade: string) {
  return `rgb(var(--surface-${shade}) / <alpha-value>)`;
}

const config: Config = {
  content: ["./src/**/*.{js,ts,jsx,tsx,mdx}"],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        brand: {
          50: "#fef7ec",
          100: "#fcecc9",
          200: "#f9d68e",
          300: "#f5b944",
          400: "#f2a01b",
          500: "#e8850c",
          600: "#cc6307",
          700: "#a9440a",
          800: "#8a360e",
          900: "#722e0f",
          950: "#421504",
        },
        surface: {
          0: surfaceColor("0"),
          50: surfaceColor("50"),
          100: surfaceColor("100"),
          200: surfaceColor("200"),
          300: surfaceColor("300"),
          400: surfaceColor("400"),
          500: surfaceColor("500"),
          600: surfaceColor("600"),
          700: surfaceColor("700"),
          800: surfaceColor("800"),
          900: surfaceColor("900"),
          950: surfaceColor("950"),
        },
      },
      fontFamily: {
        sans: ['"DM Sans"', "system-ui", "sans-serif"],
        display: ['"Instrument Sans"', "system-ui", "sans-serif"],
        mono: ['"JetBrains Mono"', "monospace"],
      },
    },
  },
  plugins: [],
};

export default config;
