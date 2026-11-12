import { Component, computed, inject, input, output } from '@angular/core';
import { CatalogStore } from '../../core/catalog.store';
import { ConditionNode, DefinitionNode, GroupNode } from '../../core/models';
import { ConditionRow } from '../condition-row/condition-row';

const MAX_UI_DEPTH = 3;

/** קבוצת תנאים (וגם / או). רקורסיבית: קבוצה יכולה להכיל תנאים וקבוצות משנה. */
@Component({
  selector: 'app-condition-group',
  imports: [ConditionRow],
  templateUrl: './condition-group.html',
  styleUrl: './condition-group.css',
})
export class ConditionGroup {
  private readonly store = inject(CatalogStore);

  readonly node = input.required<GroupNode>();
  readonly rootEntity = input.required<string>();
  readonly depth = input(0);
  readonly removable = input(false);
  readonly remove = output<void>();
  readonly changed = output<void>();

  protected readonly canNest = computed(() => this.depth() < MAX_UI_DEPTH);

  protected asCondition(n: DefinitionNode) { return n as ConditionNode; }
  protected asGroup(n: DefinitionNode) { return n as GroupNode; }

  protected setOperator(op: 'and' | 'or') {
    this.node().operator = op;
    this.changed.emit();
  }

  protected addCondition() {
    const first = this.store.fieldOptions(this.rootEntity())[0];
    if (!first) return;
    const op = this.store.catalog()?.operators[first.type][0]?.key ?? 'eq';
    this.node().items.push({ type: 'condition', field: { path: [...first.ref.path], field: first.ref.field }, operator: op });
    this.changed.emit();
  }

  protected addGroup() {
    this.node().items.push({ type: 'group', operator: this.node().operator === 'and' ? 'or' : 'and', items: [] });
    this.changed.emit();
  }

  protected removeAt(index: number) {
    this.node().items.splice(index, 1);
    this.changed.emit();
  }
}
