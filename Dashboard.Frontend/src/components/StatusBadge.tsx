const statusStyles: Record<string, string> = {
  active: 'bg-green-100 text-green-800',
  completed: 'bg-green-100 text-green-800',
  running: 'bg-blue-100 text-blue-800',
  collected: 'bg-purple-100 text-purple-800',
  paused: 'bg-yellow-100 text-yellow-800',
  failed: 'bg-red-100 text-red-800',
  archived: 'bg-gray-100 text-gray-800',
};

export default function StatusBadge({ status }: { status: string }) {
  const style = statusStyles[status] ?? 'bg-gray-100 text-gray-800';
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium capitalize ${style}`}>
      {status}
    </span>
  );
}