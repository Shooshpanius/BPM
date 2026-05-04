interface Props {
    onOpenSection: (section: string) => void;
}

export function QuickActionsWidget({ onOpenSection }: Props) {
    const actions = [
        { label: 'Создать задачу', section: 'tasks', icon: '✓' },
        { label: 'Запустить процесс', section: 'bpm-processes', icon: '▶' },
        { label: 'Мои процессы', section: 'bpm-my-processes', icon: '⚙' },
        { label: 'Контакты', section: 'contacts', icon: '👥' },
    ];

    return (
        <div className="portal-widget__content">
            <div className="widget-quick-actions">
                {actions.map(a => (
                    <button
                        key={a.section}
                        className="widget-quick-btn"
                        onClick={() => onOpenSection(a.section)}
                    >
                        <span className="widget-quick-icon">{a.icon}</span>
                        <span>{a.label}</span>
                    </button>
                ))}
            </div>
        </div>
    );
}
