export type FieldType = 'string' | 'number' | 'date' | 'boolean';
export type LogicalOperator = 'and' | 'or';

export interface CatalogField {
  key: string;
  displayName: string;
  description?: string;
  type: FieldType;
  isRequired: boolean;
}

export interface CatalogRelation {
  key: string;
  displayName: string;
  targetEntity: string;
}

export interface CatalogEntity {
  key: string;
  displayName: string;
  pluralName: string;
  description?: string;
  isQueryable: boolean;
  fields: CatalogField[];
  relations: CatalogRelation[];
}

export interface OperatorOption {
  key: string;
  label: string;
}

export interface Catalog {
  entities: CatalogEntity[];
  operators: Record<FieldType, OperatorOption[]>;
}

export interface FieldRef {
  path: string[];
  field: string;
}

export interface ConditionNode {
  type: 'condition';
  field: FieldRef;
  operator: string;
  value?: unknown;
  values?: unknown[];
  compareField?: FieldRef;
}

export interface GroupNode {
  type: 'group';
  operator: LogicalOperator;
  items: DefinitionNode[];
}

export type DefinitionNode = ConditionNode | GroupNode;

export interface ControlDefinition {
  rootEntity: string;
  root: GroupNode;
}

export interface LastRun {
  ranAtUtc: string;
  violationCount: number | null;
  succeeded: boolean;
  errorMessage?: string;
}

export interface ControlView {
  id: number;
  name: string;
  description?: string;
  definition: ControlDefinition;
  rootEntityName: string;
  summary: string;
  source: 'manual' | 'ai';
  createdAt: string;
  updatedAt: string;
  lastRun?: LastRun;
}

export interface ControlInput {
  name: string;
  description?: string;
  definition: ControlDefinition;
  source?: 'manual' | 'ai';
}

export interface RunColumn {
  key: string;
  label: string;
  type: FieldType;
}

export interface RunRow {
  values: Record<string, unknown>;
  reasons: string[];
}

export interface RunResult {
  controlId?: number;
  controlName: string;
  columns: RunColumn[];
  rows: RunRow[];
  totalCount: number;
  truncated: boolean;
  durationMs: number;
  generatedSql: string;
  ranAtUtc: string;
}

export interface AiSuggestion {
  name: string;
  description?: string;
  definition: ControlDefinition;
  summary: string;
  explanation?: string;
  confidence: number;
  warnings: string[];
  validationErrors: string[];
  isValid: boolean;
}

export const emptyDefinition = (rootEntity: string): ControlDefinition => ({
  rootEntity,
  root: { type: 'group', operator: 'and', items: [] },
});
