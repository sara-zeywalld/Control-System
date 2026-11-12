import { Component, computed, inject, input, output } from '@angular/core';
import { CatalogStore, FieldOption, fieldId } from '../../core/catalog.store';
import { ConditionNode, FieldRef, FieldType } from '../../core/models';

const COMPARABLE = ['eq', 'neq', 'gt', 'gte', 'lt', 'lte'];

@Component({
  selector: 'app-condition-row',
  templateUrl: './condition-row.html',
  styleUrl: './condition-row.css',
})
export class ConditionRow {
  private readonly store = inject(CatalogStore);

  readonly node = input.required<ConditionNode>();
  readonly rootEntity = input.required<string>();
  readonly remove = output<void>();
  readonly changed = output<void>();

  protected readonly options = computed(() => this.store.fieldOptions(this.rootEntity()));
  protected readonly groups = computed(() => [...new Set(this.options().map((o) => o.group))]);

  protected optionsOf(group: string): FieldOption[] {
    return this.options().filter((o) => o.group === group);
  }

  protected get type(): FieldType {
    return this.store.field(this.rootEntity(), this.node().field)?.type ?? 'string';
  }

  protected get operators() {
    return this.store.catalog()?.operators[this.type] ?? [];
  }

  protected get compareOptions(): FieldOption[] {
    return this.options().filter((o) => o.type === this.type && fieldId(o.ref) !== fieldId(this.node().field));
  }

  protected get needsValue() { return !['isNull', 'isNotNull'].includes(this.node().operator); }
  protected get isBetween() { return this.node().operator === 'between'; }
  protected get isList() { return ['in', 'notIn'].includes(this.node().operator); }
  protected get canCompareField() { return COMPARABLE.includes(this.node().operator) && this.compareOptions.length > 0; }
  protected get isCompareMode() { return !!this.node().compareField; }
  protected get fieldId() { return fieldId(this.node().field); }
  protected get compareId() { return this.node().compareField ? fieldId(this.node().compareField!) : ''; }
  protected get listText() { return (this.node().values ?? []).join(', '); }

  protected refOf(id: string): FieldRef | undefined {
    return this.options().find((o) => fieldId(o.ref) === id)?.ref;
  }

  protected setField(id: string) {
    const ref = this.refOf(id);
    if (!ref) return;
    const n = this.node();
    n.field = { ...ref, path: [...ref.path] };
    if (!this.operators.some((o) => o.key === n.operator)) n.operator = this.operators[0]?.key ?? 'eq';
    this.resetValues();
    this.changed.emit();
  }

  protected setOperator(op: string) {
    const n = this.node();
    n.operator = op;
    if (!COMPARABLE.includes(op)) delete n.compareField;
    this.resetValues();
    this.changed.emit();
  }

  protected toggleCompare(on: boolean) {
    const n = this.node();
    if (on) {
      const first = this.compareOptions[0];
      n.compareField = { ...first.ref, path: [...first.ref.path] };
      delete n.value; delete n.values;
    } else {
      delete n.compareField;
    }
    this.changed.emit();
  }

  protected setCompareField(id: string) {
    const ref = this.refOf(id);
    if (ref) { this.node().compareField = { ...ref, path: [...ref.path] }; this.changed.emit(); }
  }

  protected setValue(raw: string) {
    this.node().value = this.parse(raw);
    this.changed.emit();
  }

  protected setBetween(index: 0 | 1, raw: string) {
    const n = this.node();
    const values = [...(n.values ?? [undefined, undefined])];
    values[index] = this.parse(raw);
    n.values = values;
    this.changed.emit();
  }

  protected setList(raw: string) {
    this.node().values = raw.split(',').map((s) => s.trim()).filter(Boolean).map((s) => this.parse(s));
    this.changed.emit();
  }

  private resetValues() {
    const n = this.node();
    delete n.value; delete n.values;
    if (n.compareField && this.type !== this.store.field(this.rootEntity(), n.compareField)?.type) delete n.compareField;
  }

  private parse(raw: string): unknown {
    if (raw === '') return undefined;
    if (this.type === 'number') return Number(raw);
    if (this.type === 'boolean') return raw === 'true' ? true : raw === 'false' ? false : undefined;
    return raw;
  }
}
