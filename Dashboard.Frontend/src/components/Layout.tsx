import { NavLink, Outlet } from 'react-router-dom';

const navItems = [
  { to: '/', label: 'Dashboard', icon: '📊', end: true },
  { to: '/discovery/keywords', label: 'Keywords', icon: '🔍' },
  { to: '/discovery/jobs', label: 'Discovery Jobs', icon: '⚡' },
  { to: '/collector/videos', label: 'Videos', icon: '🎬' },
  { to: '/collector/jobs', label: 'Collection Jobs', icon: '📥' },
  { to: '/knowledge-extraction/jobs', label: 'Knowledge Extraction', icon: '🧠' },
  { to: '/workflow', label: 'Workflow', icon: '🔗' },
];

export default function Layout() {
  return (
    <div className="min-h-screen flex">
      <aside className="w-64 bg-gray-900 text-white flex flex-col shrink-0">
        <div className="p-5 border-b border-gray-800">
          <h1 className="text-lg font-bold">Trend Monitor</h1>
          <p className="text-xs text-gray-400 mt-1">AI Content Factory</p>
        </div>
        <nav className="flex-1 py-4">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                `flex items-center gap-3 px-5 py-2.5 text-sm transition-colors ${
                  isActive
                    ? 'bg-primary-600 text-white border-l-4 border-primary-400'
                    : 'text-gray-400 hover:bg-gray-800 hover:text-white border-l-4 border-transparent'
                }`
              }
            >
              <span className="text-base">{item.icon}</span>
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="p-5 border-t border-gray-800">
          <p className="text-xs text-gray-500">Auto-refresh 30s</p>
        </div>
      </aside>
      <main className="flex-1 p-6 overflow-x-hidden">
        <Outlet />
      </main>
    </div>
  );
}