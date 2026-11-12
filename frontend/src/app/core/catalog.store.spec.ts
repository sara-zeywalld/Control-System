import { TestBed } from '@angular/core/testing';
import { CatalogStore, fieldId } from './catalog.store';
import { Catalog } from './models';

const catalog: Catalog = {
  operators: { string: [], number: [], date: [], boolean: [] },
  entities: [
    {
      key: 'employee', displayName: 'עובד', pluralName: 'עובדים', isQueryable: true,
      fields: [{ key: 'name', displayName: 'שם', type: 'string', isRequired: true }],
      relations: [
        { key: 'department', displayName: 'מחלקה', targetEntity: 'department' },
        { key: 'manager', displayName: 'מנהל', targetEntity: 'employee' },
      ],
    },
    {
      key: 'department', displayName: 'מחלקה', pluralName: 'מחלקות', isQueryable: true,
      fields: [{ key: 'isActive', displayName: 'פעילה', type: 'boolean', isRequired: true }],
      relations: [{ key: 'site', displayName: 'אתר', targetEntity: 'site' }],
    },
    {
      key: 'site', displayName: 'אתר', pluralName: 'אתרים', isQueryable: false,
      fields: [{ key: 'city', displayName: 'עיר', type: 'string', isRequired: false }],
      relations: [{ key: 'department', displayName: 'מחלקה', targetEntity: 'department' }],
    },
  ],
};

describe('CatalogStore.fieldOptions', () => {
  let store: CatalogStore;

  beforeEach(() => {
    store = TestBed.inject(CatalogStore);
    store.catalog.set(catalog);
  });

  it('returns an empty list for an unknown entity', () => {
    expect(store.fieldOptions('missing')).toEqual([]);
  });

  it('includes own fields and fields of related entities with business labels', () => {
    const ids = store.fieldOptions('employee').map((o) => o.id);
    expect(ids).toContain('name');
    expect(ids).toContain('department/isActive');
    expect(store.fieldOptions('employee').find((o) => o.id === 'manager/name')?.label).toBe('מנהל › שם');
  });

  it('supports two-hop paths and stops at the maximum depth', () => {
    const options = store.fieldOptions('employee');
    const nested = options.find((o) => o.id === 'department/site/city');
    expect(nested?.ref).toEqual({ path: ['department', 'site'], field: 'city' });
    expect(nested?.label).toBe('מחלקה › אתר › עיר');
    expect(options.every((o) => o.ref.path.length <= 2)).toBe(true);
  });

  it('does not repeat the same relation back-to-back (manager of manager)', () => {
    expect(store.fieldOptions('employee').some((o) => o.id.startsWith('manager/manager'))).toBe(false);
  });

  it('resolves a nested field reference to its catalog field', () => {
    const field = store.field('employee', { path: ['department', 'site'], field: 'city' });
    expect(field?.displayName).toBe('עיר');
  });

  it('builds field ids from path and field', () => {
    expect(fieldId({ path: ['a', 'b'], field: 'c' })).toBe('a/b/c');
  });
});
