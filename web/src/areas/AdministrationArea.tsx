export interface AdminSummary {
  tenant: string;
  areas: string[];
  readModelOnly: boolean;
}

export function AdministrationArea({ summary }: { summary: AdminSummary }) {
  return (
    <section>
      <h2>Administration</h2>
      <div data-testid="admin-tenant">Tenant: {summary.tenant}</div>
      <div data-testid="admin-areas">Areas: {summary.areas.join(", ")}</div>
      <div data-testid="admin-read-model-only">Read-model-only: {summary.readModelOnly ? "yes" : "no"}</div>
    </section>
  );
}
