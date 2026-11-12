import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CatalogStore } from '../../core/catalog.store';
import { Catalog, ConditionNode } from '../../core/models';
import { ConditionRow } from './condition-row';

const catalog: Catalog = {
  operators: {
    string: [{ key: 'eq', label: 'שווה' }, { key: 'isNull', label: 'ריק' }],
    number: [{ key: 'eq', label: 'שווה' }, { key: 'lt', label: 'קטן מ' }, { key: 'between', label: 'בין' }],
    date: [{ key: 'eq', label: 'שווה' }],
    boolean: [{ key: 'eq', label: 'שווה' }],
  },
  entities: [
    {
      key: 'employee', displayName: 'עובד', pluralName: 'עובדים', isQueryable: true,
      fields: [
        { key: 'name', displayName: 'שם', type: 'string', isRequired: true },
        { key: 'age', displayName: 'גיל', type: 'number', isRequired: true },
        { key: 'salary', displayName: 'שכר', type: 'number', isRequired: true },
      ],
      relations: [],
    },
  ],
};

describe('ConditionRow', () => {
  let fixture: ComponentFixture<ConditionRow>;
  let node: ConditionNode;
  let changes: number;

  beforeEach(() => {
    TestBed.inject(CatalogStore).catalog.set(catalog);
    node = { type: 'condition', field: { path: [], field: 'age' }, operator: 'lt', value: 18 };
    changes = 0;
    fixture = TestBed.createComponent(ConditionRow);
    fixture.componentRef.setInput('node', node);
    fixture.componentRef.setInput('rootEntity', 'employee');
    fixture.componentInstance.changed.subscribe(() => changes++);
    fixture.detectChanges();
  });

  const row = () => fixture.componentInstance as unknown as Record<string, (...a: unknown[]) => void>;

  it('renders a numeric input for a number field', () => {
    const input = fixture.nativeElement.querySelector('input.val') as HTMLInputElement;
    expect(input.type).toBe('number');
  });

  it('parses numeric input into a number and notifies the parent', () => {
    row()['setValue']('15000');
    expect(node.value).toBe(15000);
    expect(changes).toBe(1);
  });

  it('keeps the operator and clears the value when switching to a field of another type', () => {
    row()['setField']('name');
    expect(node.field.field).toBe('name');
    expect(node.operator).toBe('eq');
    expect(node.value).toBeUndefined();
  });

  it('builds a between range from two inputs', () => {
    row()['setOperator']('between');
    row()['setBetween'](0, '10');
    row()['setBetween'](1, '20');
    expect(node.values).toEqual([10, 20]);
  });

  it('switches to field comparison mode with another field of the same type', () => {
    row()['setOperator']('lt');
    row()['toggleCompare'](true);
    expect(node.compareField?.field).toBe('salary');
    expect(node.value).toBeUndefined();
  });
});
