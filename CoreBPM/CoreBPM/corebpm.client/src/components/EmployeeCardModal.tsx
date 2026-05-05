import type { DirectoryEmployeeDto } from '../api/orgDirectoryApi';
import { useModalShake } from '../hooks/useModalShake';
import './EmployeeCardModal.css';

interface EmployeeCardModalProps {
    employee: DirectoryEmployeeDto;
    onClose: () => void;
}

/** Модальное окно — карточка сотрудника (FR-ORG-04.2). */
export function EmployeeCardModal({ employee, onClose }: EmployeeCardModalProps) {
    const { shaking, shake } = useModalShake();
    const initials = (
        (employee.firstName?.[0] ?? '') + (employee.lastName?.[0] ?? '')
    ).toUpperCase() || employee.displayName[0].toUpperCase();

    return (
        <div className="ecm-backdrop" onClick={(e) => { if (e.target === e.currentTarget) shake(); }} role="dialog" aria-modal="true">
            <div className="ecm-modal">
                <button className={`ecm-close${shaking ? ' btn-flash' : ''}`} onClick={onClose} aria-label="Закрыть">✕</button>

                <div className="ecm-avatar">
                    {employee.avatarUrl
                        ? <img src={employee.avatarUrl} alt={employee.displayName} className="ecm-avatar-img" />
                        : <span className="ecm-avatar-initials">{initials}</span>
                    }
                </div>

                <h2 className="ecm-name">{employee.displayName}</h2>
                {employee.position && <p className="ecm-position">{employee.position}</p>}

                <div className="ecm-info">
                    {employee.departmentName && (
                        <div className="ecm-info-row">
                            <span className="ecm-info-label">Подразделение</span>
                            <span className="ecm-info-value">{employee.departmentName}</span>
                        </div>
                    )}
                    <div className="ecm-info-row">
                        <span className="ecm-info-label">Организация</span>
                        <span className="ecm-info-value">{employee.organizationName}</span>
                    </div>
                    <div className="ecm-info-row">
                        <span className="ecm-info-label">Email</span>
                        <a className="ecm-info-link" href={`mailto:${employee.workEmail}`}>{employee.workEmail}</a>
                    </div>
                    {employee.phone && (
                        <div className="ecm-info-row">
                            <span className="ecm-info-label">Телефон</span>
                            <a className="ecm-info-link" href={`tel:${employee.phone}`}>{employee.phone}</a>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
