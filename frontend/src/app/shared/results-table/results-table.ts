import { Component, computed, input, signal } from '@angular/core';
import { RunColumn, RunResult, RunRow } from '../../core/models';

const PAGE_SIZES = [10, 25, 50, 100];

@Component({
  selector: 'app-results-table',
  templateUrl: './results-table.html',
  styleUrl: './results-table.css',
})
export class ResultsTable {
  readonly result = input.required<RunResult>();

  protected readonly pageSizes = PAGE_SIZES;
  protected readonly search = signal('');
  protected readonly columnFilters = signal<Record<string, string>>({});
  protected readonly sortKey = signal<string | null>(null);
  protected readonly sortDesc = signal(false);
  protected readonly pageSize = signal(25);
  protected readonly page = signal(0);
  protected readonly showFilters = signal(false);
  protected readonly filtersActive = computed(() => Object.values(this.columnFilters()).some((v) => v.trim()));

  protected readonly filtered = computed(() => {
    const cols = this.result().columns;
    const q = this.search().trim().toLowerCase();
    const filters = Object.entries(this.columnFilters()).filter(([, v]) => v.trim());
    let rows = this.result().rows.filter((row) => {
      if (q && !this.rowText(row, cols).includes(q)) return false;
      return filters.every(([key, v]) => {
        const col = cols.find((c) => c.key === key);
        return col && this.format(row.values[key], col).toLowerCase().includes(v.trim().toLowerCase());
      });
    });
    const key = this.sortKey();
    if (key) {
      const dir = this.sortDesc() ? -1 : 1;
      rows = [...rows].sort((a, b) => dir * this.compare(a.values[key], b.values[key]));
    }
    return rows;
  });

  protected readonly pageCount = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize())));
  protected readonly pageRows = computed(() => {
    const start = Math.min(this.page(), this.pageCount() - 1) * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });
  protected readonly currentPage = computed(() => Math.min(this.page(), this.pageCount() - 1));

  protected setSearch(v: string) { this.search.set(v); this.page.set(0); }

  protected setFilter(key: string, v: string) {
    this.columnFilters.update((f) => ({ ...f, [key]: v }));
    this.page.set(0);
  }

  protected clearFilters() {
    this.search.set('');
    this.columnFilters.set({});
    this.page.set(0);
  }

  protected sortBy(col: RunColumn) {
    if (this.sortKey() === col.key) this.sortDesc.update((d) => !d);
    else { this.sortKey.set(col.key); this.sortDesc.set(false); }
  }

  protected setPageSize(v: string) { this.pageSize.set(+v); this.page.set(0); }

  protected format(value: unknown, col: RunColumn): string {
    if (value === null || value === undefined) return '—';
    switch (col.type) {
      case 'boolean': return value ? 'כן' : 'לא';
      case 'date': return new Date(value as string).toLocaleDateString('he-IL');
      case 'number': return typeof value === 'number' ? value.toLocaleString('he-IL') : String(value);
      default: return String(value);
    }
  }

  private rowText(row: RunRow, cols: RunColumn[]): string {
    return [...cols.map((c) => this.format(row.values[c.key], c)), ...row.reasons].join(' ').toLowerCase();
  }

  private compare(a: unknown, b: unknown): number {
    if (a == null) return b == null ? 0 : 1;
    if (b == null) return -1;
    if (typeof a === 'number' && typeof b === 'number') return a - b;
    return String(a).localeCompare(String(b), 'he');
  }
}
