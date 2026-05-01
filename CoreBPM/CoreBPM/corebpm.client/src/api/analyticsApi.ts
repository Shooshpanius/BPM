// API-клиент для аналитики и KPI бизнес-процессов (FR-BPM-03.2)

// ─── DTO ─────────────────────────────────────────────────────────────────────

export interface CycleTimeHistogramBucketDto {
    fromMinutes: number;
    toMinutes: number;
    count: number;
}

export interface ProcessAnalyticsDto {
    processId: string;
    processName: string;
    totalInstances: number;
    completedInstances: number;
    faultedInstances: number;
    onTimePercent: number;
    faultedPercent: number;
    avgCycleTimeMinutes: number;
    medianCycleTimeMinutes: number;
    p95CycleTimeMinutes: number;
    cycleTimeHistogram: CycleTimeHistogramBucketDto[];
    targetCycleTimeMinutes?: number;
    targetOnTimePercent?: number;
}

export interface ProcessVersionComparisonDto {
    processId: string;
    processName: string;
    versionAId: string;
    versionANumber: number;
    versionBId: string;
    versionBNumber: number;
    versionAAnalytics: ProcessAnalyticsDto;
    versionBAnalytics: ProcessAnalyticsDto;
}

export interface ProcessAnalyticsSummaryItemDto {
    processId: string;
    processName: string;
    totalInstances: number;
    avgCycleTimeMinutes: number;
    onTimePercent: number;
    faultedPercent: number;
    targetCycleTimeMinutes?: number;
    targetOnTimePercent?: number;
}

export interface NodeHeatMapDto {
    elementId: string;
    elementName: string;
    avgDurationMs: number;
    passCount: number;
    heatLevel: number;
}

export interface ProcessFunnelStepDto {
    elementId: string;
    elementName: string;
    reachedCount: number;
    passedCount: number;
    dropOffPercent: number;
}

// ─── Вспомогательная функция ─────────────────────────────────────────────────

async function apiFetch<T>(url: string, token: string): Promise<T> {
    const res = await fetch(url, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) {
        const text = await res.text().catch(() => '');
        throw new Error(text || `HTTP ${res.status}`);
    }
    return res.json();
}

function buildQs(params: Record<string, string | undefined | null>): string {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) {
        if (v != null && v !== '') qs.append(k, v);
    }
    const s = qs.toString();
    return s ? `?${s}` : '';
}

// ─── API-функции ─────────────────────────────────────────────────────────────

export const getProcessAnalytics = (
    token: string,
    processId: string,
    from?: string,
    to?: string,
): Promise<ProcessAnalyticsDto> =>
    apiFetch(
        `/api/analytics/processes/${processId}${buildQs({ from, to })}`,
        token,
    );

export const getNodeHeatMap = (
    token: string,
    processId: string,
    versionId?: string,
    from?: string,
    to?: string,
): Promise<NodeHeatMapDto[]> =>
    apiFetch(
        `/api/analytics/processes/${processId}/heatmap${buildQs({ versionId, from, to })}`,
        token,
    );

export const getProcessFunnel = (
    token: string,
    processId: string,
    versionId?: string,
    from?: string,
    to?: string,
): Promise<ProcessFunnelStepDto[]> =>
    apiFetch(
        `/api/analytics/processes/${processId}/funnel${buildQs({ versionId, from, to })}`,
        token,
    );

export const getVersionComparison = (
    token: string,
    processId: string,
    versionAId: string,
    versionBId: string,
    from?: string,
    to?: string,
): Promise<ProcessVersionComparisonDto> =>
    apiFetch(
        `/api/analytics/processes/${processId}/version-comparison${buildQs({ versionAId, versionBId, from, to })}`,
        token,
    );

export const getAnalyticsSummary = (
    token: string,
    from?: string,
    to?: string,
): Promise<ProcessAnalyticsSummaryItemDto[]> =>
    apiFetch(
        `/api/analytics/summary${buildQs({ from, to })}`,
        token,
    );
