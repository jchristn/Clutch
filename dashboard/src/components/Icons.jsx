// Minimal stroke-icon set. Each icon takes a `size` prop and inherits color.

const base = (size) => ({
  width: size,
  height: size,
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 2,
  strokeLinecap: 'round',
  strokeLinejoin: 'round'
});

export function HomeIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <path d="M3 10.5 12 3l9 7.5" />
      <path d="M5 9.5V21h14V9.5" />
    </svg>
  );
}
export function LockIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <rect x="4" y="10" width="16" height="11" rx="2" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" />
    </svg>
  );
}
export function ActivityIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <path d="M3 12h4l3 8 4-16 3 8h4" />
    </svg>
  );
}
export function BuildingIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <rect x="4" y="3" width="16" height="18" rx="1" />
      <path d="M9 8h.01M15 8h.01M9 12h.01M15 12h.01M9 16h6" />
    </svg>
  );
}
export function UsersIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <circle cx="9" cy="8" r="3" />
      <path d="M3 20a6 6 0 0 1 12 0" />
      <path d="M16 5.5a3 3 0 0 1 0 5.4M21 20a6 6 0 0 0-4-5.6" />
    </svg>
  );
}
export function KeyIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <circle cx="7.5" cy="15.5" r="4.5" />
      <path d="M10.5 12.5 20 3M17 6l2 2M14 9l2 2" />
    </svg>
  );
}
export function ListIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" />
    </svg>
  );
}
export function PlayIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <path d="M6 4l14 8-14 8z" />
    </svg>
  );
}
export function ServerIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <rect x="3" y="4" width="18" height="7" rx="1.5" />
      <rect x="3" y="13" width="18" height="7" rx="1.5" />
      <path d="M7 7.5h.01M7 16.5h.01" />
    </svg>
  );
}
export function CopyIcon({ size = 14 }) {
  return (
    <svg {...base(size)}>
      <rect x="9" y="9" width="11" height="11" rx="2" />
      <path d="M5 15V5a2 2 0 0 1 2-2h8" />
    </svg>
  );
}
export function CheckIcon({ size = 14 }) {
  return (
    <svg {...base(size)} stroke="var(--color-success)">
      <path d="M20 6 9 17l-5-5" />
    </svg>
  );
}
export function TrashIcon({ size = 16 }) {
  return (
    <svg {...base(size)}>
      <path d="M4 7h16M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2M6 7l1 13a1 1 0 0 0 1 1h8a1 1 0 0 0 1-1l1-13" />
    </svg>
  );
}
export function EyeIcon({ size = 16 }) {
  return (
    <svg {...base(size)}>
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );
}
export function EyeOffIcon({ size = 16 }) {
  return (
    <svg {...base(size)}>
      <path d="M3 3l18 18M10.6 10.6a3 3 0 0 0 4.2 4.2M9.9 5.2A9.6 9.6 0 0 1 12 5c6.5 0 10 7 10 7a17 17 0 0 1-3.2 4M6.1 6.1A17 17 0 0 0 2 12s3.5 7 10 7a9.5 9.5 0 0 0 2.1-.2" />
    </svg>
  );
}
export function CodeIcon({ size = 16 }) {
  return (
    <svg {...base(size)}>
      <path d="M8 8l-4 4 4 4M16 8l4 4-4 4" />
    </svg>
  );
}
export function RefreshIcon({ size = 14 }) {
  return (
    <svg {...base(size)}>
      <path d="M4 12a8 8 0 0 1 13.5-5.8L21 9M20 12a8 8 0 0 1-13.5 5.8L3 15" />
      <path d="M21 4v5h-5M3 20v-5h5" />
    </svg>
  );
}
export function ChevronLeftIcon({ size = 14 }) {
  return (
    <svg {...base(size)}>
      <path d="M15 6l-6 6 6 6" />
    </svg>
  );
}
export function ChevronRightIcon({ size = 14 }) {
  return (
    <svg {...base(size)}>
      <path d="M9 6l6 6-6 6" />
    </svg>
  );
}
export function ChevronsLeftIcon({ size = 14 }) {
  return (
    <svg {...base(size)}>
      <path d="M18 6l-6 6 6 6M12 6l-6 6 6 6" />
    </svg>
  );
}
export function ChevronsRightIcon({ size = 14 }) {
  return (
    <svg {...base(size)}>
      <path d="M6 6l6 6-6 6M12 6l6 6-6 6" />
    </svg>
  );
}
export function ChevronDownIcon({ size = 14 }) {
  return (
    <svg {...base(size)}>
      <path d="M6 9l6 6 6-6" />
    </svg>
  );
}
export function MoreVerticalIcon({ size = 16 }) {
  return (
    <svg {...base(size)}>
      <circle cx="12" cy="5" r="1.4" />
      <circle cx="12" cy="12" r="1.4" />
      <circle cx="12" cy="19" r="1.4" />
    </svg>
  );
}
export function MenuIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <path d="M4 6h16M4 12h16M4 18h16" />
    </svg>
  );
}
export function SunIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4 12H2M22 12h-2M5 5l1.5 1.5M17.5 17.5 19 19M19 5l-1.5 1.5M6.5 17.5 5 19" />
    </svg>
  );
}
export function MoonIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <path d="M21 12.8A8.5 8.5 0 1 1 11.2 3a6.5 6.5 0 0 0 9.8 9.8z" />
    </svg>
  );
}
export function LogoutIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <path d="M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3" />
      <path d="M10 17l-5-5 5-5M5 12h11" />
    </svg>
  );
}
export function GithubIcon({ size = 18 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor">
      <path d="M12 2C6.48 2 2 6.58 2 12.25c0 4.53 2.87 8.37 6.84 9.73.5.1.68-.22.68-.49 0-.24-.01-.87-.01-1.71-2.78.62-3.37-1.22-3.37-1.22-.46-1.18-1.11-1.5-1.11-1.5-.9-.63.07-.62.07-.62 1 .07 1.53 1.05 1.53 1.05.89 1.56 2.34 1.11 2.91.85.09-.66.35-1.11.63-1.37-2.22-.26-4.55-1.14-4.55-5.06 0-1.12.39-2.03 1.03-2.75-.1-.26-.45-1.3.1-2.7 0 0 .84-.28 2.75 1.05a9.3 9.3 0 0 1 5 0c1.91-1.33 2.75-1.05 2.75-1.05.55 1.4.2 2.44.1 2.7.64.72 1.03 1.63 1.03 2.75 0 3.93-2.34 4.79-4.57 5.05.36.32.68.94.68 1.9 0 1.37-.01 2.48-.01 2.82 0 .27.18.6.69.49A10.02 10.02 0 0 0 22 12.25C22 6.58 17.52 2 12 2z" />
    </svg>
  );
}
export function XIcon({ size = 18 }) {
  return (
    <svg {...base(size)}>
      <path d="M6 6l12 12M18 6 6 18" />
    </svg>
  );
}
export function PlusIcon({ size = 16 }) {
  return (
    <svg {...base(size)}>
      <path d="M12 5v14M5 12h14" />
    </svg>
  );
}
