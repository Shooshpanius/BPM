import './Sidebar.css';

export type SidebarSection = 'contacts';

interface SidebarProps {
    active: SidebarSection;
    onSelect: (section: SidebarSection) => void;
}

/** Вертикальный сайдбар навигации (~60px) с иконками разделов. */
export function Sidebar({ active, onSelect }: SidebarProps) {
    return (
        <nav className="sidebar" aria-label="Разделы системы">
            <SidebarItem
                id="contacts"
                label="Контакты"
                active={active === 'contacts'}
                onClick={() => onSelect('contacts')}
                icon={
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/>
                        <circle cx="9" cy="7" r="4"/>
                        <path d="M23 21v-2a4 4 0 0 0-3-3.87"/>
                        <path d="M16 3.13a4 4 0 0 1 0 7.75"/>
                    </svg>
                }
            />
        </nav>
    );
}

interface SidebarItemProps {
    id: string;
    label: string;
    active: boolean;
    onClick: () => void;
    icon: React.ReactNode;
}

function SidebarItem({ label, active, onClick, icon }: SidebarItemProps) {
    return (
        <button
            className={`sidebar-item${active ? ' active' : ''}`}
            onClick={onClick}
            title={label}
            aria-label={label}
            aria-pressed={active}
        >
            <span className="sidebar-icon">{icon}</span>
            <span className="sidebar-label">{label}</span>
        </button>
    );
}
