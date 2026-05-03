import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { useMobile } from '../hooks/useMobile';
import { Sidebar, type SidebarSection } from '../components/Sidebar';
import { MobileNav } from '../components/MobileNav';
import { ContactsPage } from './contacts/ContactsPage';
import { OrgStructurePage } from './org/OrgStructurePage';
import { ProcessesPage } from './bpm/ProcessesPage';
import { BpmnDesignerPage } from './bpm/BpmnDesignerPage';
import { ProcessMonitorPage } from './bpm/ProcessMonitorPage';
import { InstancePage } from './bpm/InstancePage';
import { ProcessMonitorListPage } from './bpm/ProcessMonitorListPage';
import { MyProcessesPage } from './bpm/MyProcessesPage';
import { RulesPage } from './rules/RulesPage';
import { DmnEditorPage } from './rules/DmnEditorPage';
import { FormsPage } from './forms/FormsPage';
import { FormBuilderPage } from './forms/FormBuilderPage';
import { ScriptsPage } from './scripts/ScriptsPage';
import { ExecutionQueuePage } from './bpm/ExecutionQueuePage';
import ProcessDocumentationPage from './bpm/ProcessDocumentationPage';
import { MigrationPackagesPage } from './bpm/MigrationPackagesPage';
import { MigrationPackageDetailPage } from './bpm/MigrationPackageDetailPage';
import { ImprovementsPage } from './bpm/ImprovementsPage';
import { AnalyticsSummaryPage } from './bpm/AnalyticsSummaryPage';
import { ProcessAnalyticsPage } from './bpm/ProcessAnalyticsPage';
import TaskControlSettingsPage from './admin/TaskControlSettingsPage';
import { MyColleaguesWidget } from '../components/org/MyColleaguesWidget';
import { TasksPage } from './tasks/TasksPage';
import { TaskDetailPage } from './tasks/TaskDetailPage';
import './HomePage.css';

interface HomePageProps {
    onAdmin: () => void;
}

/** Основная страница приложения: шапка + сайдбар/нижняя навигация + содержимое раздела. */
export function HomePage({ onAdmin }: HomePageProps) {
    const { logout, hasRole } = useAuth();
    const canManageOrg = hasRole('Admin') || hasRole('HR');
    const isMobile = useMobile();
    const [section, setSection] = useState<SidebarSection>('tasks');
    const [designerProcessId, setDesignerProcessId] = useState<string | null>(null);
    const [monitorProcess, setMonitorProcess] = useState<{ id: string; name: string } | null>(null);
    const [openInstanceId, setOpenInstanceId] = useState<string | null>(null);
    const [dmnEditorTableId, setDmnEditorTableId] = useState<string | null>(null);
    const [formBuilderId, setFormBuilderId] = useState<string | null>(null);
    const [migrationPackageId, setMigrationPackageId] = useState<string | null>(null);
    const [analyticsProcess, setAnalyticsProcess] = useState<{ id: string; name: string } | null>(null);
    const [openTaskId, setOpenTaskId] = useState<string | null>(null);

    const handleSelect = (s: SidebarSection) => {
        // Обычные пользователи не имеют доступа к разделу «Оргструктура»
        if (s === 'org-structure' && !canManageOrg) return;
        setDesignerProcessId(null);
        setMonitorProcess(null);
        setDmnEditorTableId(null);
        setFormBuilderId(null);
        setOpenInstanceId(null);
        setMigrationPackageId(null);
        setAnalyticsProcess(null);
        setOpenTaskId(null);
        setSection(s);
    };

    const handleOpenDesigner = (processId: string) => {
        setMonitorProcess(null);
        setOpenInstanceId(null);
        setDesignerProcessId(processId);
    };

    const handleOpenMonitor = (processId: string, processName: string) => {
        setDesignerProcessId(null);
        setOpenInstanceId(null);
        setMonitorProcess({ id: processId, name: processName });
    };

    const handleOpenInstance = (instanceId: string) => {
        setOpenInstanceId(instanceId);
    };

    const handleBackFromInstance = () => {
        setOpenInstanceId(null);
    };

    const handleOpenTaskFromInstance = (taskId: string) => {
        setSection('tasks');
        setOpenInstanceId(null);
        setOpenTaskId(taskId);
    };

    return (
        <div className={`hp-root${isMobile ? ' hp-root--mobile' : ''}`}>
            <header className="hp-header">
                <div className="hp-header-brand">
                    <span className="hp-logo-icon" aria-hidden="true">⬡</span>
                    <span className="hp-logo-name">Core BPM</span>
                </div>
                <nav className="hp-header-nav">
                    {hasRole('Admin') && (
                        <button className="hp-admin-btn" onClick={onAdmin}>
                            {isMobile ? 'Адм.' : 'Администрирование'}
                        </button>
                    )}
                    <button className="hp-logout-btn" onClick={logout}>
                        Выйти
                    </button>
                </nav>
            </header>

            <div className="hp-body">
                {!isMobile && <Sidebar active={section} onSelect={handleSelect} />}
                <main className="hp-content">
                    {section === 'tasks' && (
                        openTaskId
                            ? <TaskDetailPage taskId={openTaskId} onBack={() => setOpenTaskId(null)} />
                            : <TasksPage onOpenTask={setOpenTaskId} />
                    )}
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
                                onBack={() => setDesignerProcessId(null)}
                              />
                            : openInstanceId
                                ? <InstancePage
                                    instanceId={openInstanceId}
                                    onBack={handleBackFromInstance}
                                    onOpenTask={handleOpenTaskFromInstance}
                                  />
                                : monitorProcess
                                    ? <ProcessMonitorPage
                                        processId={monitorProcess.id}
                                        processName={monitorProcess.name}
                                        onBack={() => setMonitorProcess(null)}
                                        onOpenInstance={handleOpenInstance}
                                      />
                                    : <ProcessesPage
                                        onOpenDesigner={handleOpenDesigner}
                                        onOpenMonitor={handleOpenMonitor}
                                      />
                    )}
                    {section === 'bpm-my-processes' && (
                        openInstanceId
                            ? <InstancePage
                                instanceId={openInstanceId}
                                onBack={handleBackFromInstance}
                                onOpenTask={handleOpenTaskFromInstance}
                              />
                            : <MyProcessesPage onOpenInstance={handleOpenInstance} />
                    )}
                    {section === 'bpm-monitor' && (
                        openInstanceId
                            ? <InstancePage
                                instanceId={openInstanceId}
                                onBack={handleBackFromInstance}
                                onOpenTask={handleOpenTaskFromInstance}
                              />
                            : monitorProcess
                                ? <ProcessMonitorPage
                                    processId={monitorProcess.id}
                                    processName={monitorProcess.name}
                                    onBack={() => setMonitorProcess(null)}
                                    onOpenInstance={handleOpenInstance}
                                  />
                                : <ProcessMonitorListPage onOpenMonitor={handleOpenMonitor} />
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
                    {section === 'bpm-scripts' && <ScriptsPage />}
                    {section === 'bpm-improvements' && <ImprovementsPage />}
                    {section === 'bpm-analytics' && (
                        analyticsProcess
                            ? <ProcessAnalyticsPage
                                processId={analyticsProcess.id}
                                processName={analyticsProcess.name}
                                onBack={() => setAnalyticsProcess(null)}
                              />
                            : <AnalyticsSummaryPage
                                onOpenProcess={(id, name) => setAnalyticsProcess({ id, name })}
                              />
                    )}
                    {section === 'bpm-queue' && <ExecutionQueuePage />}
                    {section === 'bpm-documentation' && <ProcessDocumentationPage />}
                    {section === 'bpm-migration' && (
                        migrationPackageId
                            ? <MigrationPackageDetailPage
                                packageId={migrationPackageId}
                                onBack={() => setMigrationPackageId(null)}
                              />
                            : <MigrationPackagesPage onOpenDetail={setMigrationPackageId} />
                    )}
                    {section === 'task-control-settings' && <TaskControlSettingsPage />}
                </main>
            </div>
            {isMobile && <MobileNav active={section} onSelect={handleSelect} />}
        </div>
    );
}
