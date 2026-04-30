import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { Sidebar, type SidebarSection } from '../components/Sidebar';
import { ContactsPage } from './contacts/ContactsPage';
import { OrgStructurePage } from './org/OrgStructurePage';
import { ProcessesPage } from './bpm/ProcessesPage';
import { BpmnDesignerPage } from './bpm/BpmnDesignerPage';
import { RulesPage } from './rules/RulesPage';
import { DmnEditorPage } from './rules/DmnEditorPage';
import { FormsPage } from './forms/FormsPage';
import { FormBuilderPage } from './forms/FormBuilderPage';
import { MyColleaguesWidget } from '../components/org/MyColleaguesWidget';
import './HomePage.css';

interface HomePageProps {
    onAdmin: () => void;
}

/** Основная страница приложения: шапка + сайдбар + содержимое раздела. */
export function HomePage({ onAdmin }: HomePageProps) {
    const { logout, hasRole } = useAuth();
    const canManageOrg = hasRole('Admin') || hasRole('HR');
    const [section, setSection] = useState<SidebarSection>('contacts');
    const [designerProcessId, setDesignerProcessId] = useState<string | null>(null);
    const [dmnEditorTableId, setDmnEditorTableId] = useState<string | null>(null);
    const [formBuilderId, setFormBuilderId] = useState<string | null>(null);

    const handleSelect = (s: SidebarSection) => {
        // Обычные пользователи не имеют доступа к разделу «Оргструктура»
        if (s === 'org-structure' && !canManageOrg) return;
        setDesignerProcessId(null);
        setDmnEditorTableId(null);
        setFormBuilderId(null);
        setSection(s);
    };

    const handleOpenDesigner = (processId: string) => {
        setDesignerProcessId(processId);
    };

    const handleBackFromDesigner = () => {
        setDesignerProcessId(null);
    };

    return (
        <div className="hp-root">
            <header className="hp-header">
                <div className="hp-header-brand">
                    <span className="hp-logo-icon" aria-hidden="true">⬡</span>
                    <span className="hp-logo-name">Core BPM</span>
                </div>
                <nav className="hp-header-nav">
                    {hasRole('Admin') && (
                        <button className="hp-admin-btn" onClick={onAdmin}>
                            Администрирование
                        </button>
                    )}
                    <button className="hp-logout-btn" onClick={logout}>
                        Выйти
                    </button>
                </nav>
            </header>

            <div className="hp-body">
                <Sidebar active={section} onSelect={handleSelect} />
                <main className="hp-content">
                    {section === 'contacts' && (
                        <div className="hp-contacts-layout">
                            <ContactsPage />
                            <aside className="hp-aside">
                                <MyColleaguesWidget />
                            </aside>
                        </div>
                    )}
                    {section === 'org-structure' && <OrgStructurePage />}
                    {section === 'bpm-processes' && (
                        designerProcessId
                            ? <BpmnDesignerPage
                                processId={designerProcessId}
                                onBack={handleBackFromDesigner}
                              />
                            : <ProcessesPage onOpenDesigner={handleOpenDesigner} />
                    )}
                    {section === 'bpm-rules' && (
                        dmnEditorTableId
                            ? <DmnEditorPage
                                tableId={dmnEditorTableId}
                                onBack={() => setDmnEditorTableId(null)}
                              />
                            : <RulesPage onOpenEditor={setDmnEditorTableId} />
                    )}
                    {section === 'bpm-forms' && (
                        formBuilderId
                            ? <FormBuilderPage
                                formId={formBuilderId}
                                onBack={() => setFormBuilderId(null)}
                              />
                            : <FormsPage onOpenBuilder={setFormBuilderId} />
                    )}
                </main>
            </div>
        </div>
    );
}
