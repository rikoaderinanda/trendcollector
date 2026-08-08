interface StatCardProps {
  title: string;
  value: string | number;
  icon?: React.ReactNode;
  subtitle?: string;
  accentColor?: string;
}

export default function StatCard({ title, value, icon, subtitle, accentColor = 'bg-primary-50 text-primary-600' }: StatCardProps) {
  return (
    <div className="card p-5 flex items-start justify-between">
      <div>
        <p className="text-sm font-medium text-gray-500">{title}</p>
        <p className="mt-1 text-2xl font-bold text-gray-900">{value}</p>
        {subtitle && <p className="mt-1 text-xs text-gray-500">{subtitle}</p>}
      </div>
      {icon && (
        <div className={`p-3 rounded-lg ${accentColor}`}>{icon}</div>
      )}
    </div>
  );
}