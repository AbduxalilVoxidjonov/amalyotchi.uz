import type { TutorScope, TutorScopeInput, TutorScopeLevel, TutorScopeTree } from '../types';

/** `scope-tree` daraxtining yassilangan tuguni — `ScopePickerModal` uchun. */
export interface ScopeNode {
  /** `level:id` — tanlov to'plami kaliti. */
  key: string;
  level: TutorScopeLevel;
  id: string;
  name: string;
  parentKey: string | null;
  /** Kelish tartibida (fakultet → kafedra → yo'nalish → guruh, chuqurlik-birinchi). */
  depth: number;
  tutorId: string | null;
  tutorName: string | null;
  /** Guruh tuguni uchun kurs; boshqalarda null. */
  course: number | null;
  /** Ostidagi (yoki o'zining) faol guruhlar soni. */
  groups: number;
  students: number;
  childKeys: string[];
}

export function scopeKey(level: TutorScopeLevel, id: string): string {
  return `${level}:${id}`;
}

export function scopeInputOf(node: Pick<ScopeNode, 'level' | 'id'>): TutorScopeInput {
  return { level: node.level, id: node.id };
}

/** `TutorDetail.scopes` → boshlang'ich tanlov kalitlari. */
export function scopeKeysOf(scopes: readonly TutorScope[]): Set<string> {
  const ids: Record<TutorScopeLevel, keyof TutorScope> = {
    faculty: 'facultyId',
    department: 'departmentId',
    direction: 'directionId',
    group: 'groupId',
  };
  const keys = new Set<string>();
  for (const s of scopes) {
    const id = s[ids[s.level]];
    if (typeof id === 'string' && id) keys.add(scopeKey(s.level, id));
  }
  return keys;
}

/** Daraxtni yassilaydi (chuqurlik-birinchi) va har tugun uchun guruh/talaba yig'indisini hisoblaydi. */
export function flattenScopeTree(tree: TutorScopeTree): ScopeNode[] {
  const nodes: ScopeNode[] = [];
  const facultyKey = scopeKey('faculty', tree.id);
  const faculty: ScopeNode = {
    key: facultyKey,
    level: 'faculty',
    id: tree.id,
    name: tree.name,
    parentKey: null,
    depth: 0,
    tutorId: tree.tutorId,
    tutorName: tree.tutorName,
    course: null,
    groups: 0,
    students: 0,
    childKeys: [],
  };
  nodes.push(faculty);
  for (const d of tree.departments) {
    const department: ScopeNode = {
      key: scopeKey('department', d.id),
      level: 'department',
      id: d.id,
      name: d.name,
      parentKey: facultyKey,
      depth: 1,
      tutorId: d.tutorId,
      tutorName: d.tutorName,
      course: null,
      groups: 0,
      students: 0,
      childKeys: [],
    };
    faculty.childKeys.push(department.key);
    nodes.push(department);
    for (const dir of d.directions) {
      const direction: ScopeNode = {
        key: scopeKey('direction', dir.id),
        level: 'direction',
        id: dir.id,
        name: dir.name,
        parentKey: department.key,
        depth: 2,
        tutorId: dir.tutorId,
        tutorName: dir.tutorName,
        course: null,
        groups: 0,
        students: 0,
        childKeys: [],
      };
      department.childKeys.push(direction.key);
      nodes.push(direction);
      for (const g of dir.groups) {
        const group: ScopeNode = {
          key: scopeKey('group', g.id),
          level: 'group',
          id: g.id,
          name: g.name,
          parentKey: direction.key,
          depth: 3,
          tutorId: g.tutorId,
          tutorName: g.tutorName,
          course: g.course,
          groups: 1,
          students: g.students,
          childKeys: [],
        };
        direction.childKeys.push(group.key);
        nodes.push(group);
        direction.groups += 1;
        direction.students += g.students;
      }
      department.groups += direction.groups;
      department.students += direction.students;
    }
    faculty.groups += department.groups;
    faculty.students += department.students;
  }
  return nodes;
}

export type ScopeNodeState =
  | { kind: 'free' }
  /** Joriy tyutorning o'zi tanlagan (yoki boshlang'ich ko'lami). */
  | { kind: 'selected' }
  /** Tanlangan ajdodi bor — qamrab olingan ("ota orqali"), tanlovga yuborilmaydi. */
  | { kind: 'covered'; byName: string }
  /** AYNAN shu tugun boshqa tyutorniki. */
  | { kind: 'taken'; tutorName: string }
  /** Ajdodi boshqa tyutorniki — u orqali band. */
  | { kind: 'takenViaAncestor'; tutorName: string }
  /** Avlodi boshqa tyutorniki — tanlansa kesishadi. */
  | { kind: 'takenInside'; tutorName: string };

/**
 * Har tugun holatini hisoblaydi. Ustuvorlik: taken → takenViaAncestor → takenInside → selected → covered → free.
 * `tutorId` — joriy tyutor (o'z ko'lami "taken" emas).
 */
export function computeNodeStates(
  nodes: readonly ScopeNode[],
  selected: ReadonlySet<string>,
  tutorId: string,
): Map<string, ScopeNodeState> {
  const byKey = new Map(nodes.map((n) => [n.key, n]));
  const otherOwner = (n: ScopeNode) => n.tutorId !== null && n.tutorId !== tutorId;

  // Avlodlar orasida boshqa tyutor bormi — pastdan yuqoriga (nodes chuqurlik-birinchi, teskari yurish).
  const insideOther = new Map<string, string | null>();
  for (let i = nodes.length - 1; i >= 0; i -= 1) {
    const n = nodes[i]!;
    let found: string | null = null;
    for (const childKey of n.childKeys) {
      const child = byKey.get(childKey)!;
      if (otherOwner(child)) {
        found = child.tutorName;
        break;
      }
      const deeper = insideOther.get(childKey);
      if (deeper) {
        found = deeper;
        break;
      }
    }
    insideOther.set(n.key, found);
  }

  const states = new Map<string, ScopeNodeState>();
  for (const n of nodes) {
    const parent = n.parentKey ? byKey.get(n.parentKey) : undefined;
    const parentState = parent ? states.get(parent.key) : undefined;

    if (otherOwner(n)) {
      states.set(n.key, { kind: 'taken', tutorName: n.tutorName ?? '' });
      continue;
    }
    if (parentState?.kind === 'taken' || parentState?.kind === 'takenViaAncestor') {
      states.set(n.key, { kind: 'takenViaAncestor', tutorName: parentState.tutorName });
      continue;
    }
    const inside = insideOther.get(n.key);
    if (inside) {
      states.set(n.key, { kind: 'takenInside', tutorName: inside });
      continue;
    }
    if (selected.has(n.key)) {
      states.set(n.key, { kind: 'selected' });
      continue;
    }
    if (parentState?.kind === 'selected') {
      states.set(n.key, { kind: 'covered', byName: parent!.name });
      continue;
    }
    if (parentState?.kind === 'covered') {
      states.set(n.key, { kind: 'covered', byName: parentState.byName });
      continue;
    }
    states.set(n.key, { kind: 'free' });
  }
  return states;
}

/** Tugun avlodlarining kalitlari (o'zi kirmaydi). */
export function descendantKeys(nodes: readonly ScopeNode[], key: string): string[] {
  const byKey = new Map(nodes.map((n) => [n.key, n]));
  const out: string[] = [];
  const stack = [...(byKey.get(key)?.childKeys ?? [])];
  while (stack.length > 0) {
    const k = stack.pop()!;
    out.push(k);
    stack.push(...(byKey.get(k)?.childKeys ?? []));
  }
  return out;
}

/** Qidiruv: mos tugunlar va ularning ajdodlari ko'rinadi. Bo'sh so'rov — hammasi. */
export function visibleKeys(nodes: readonly ScopeNode[], q: string): Set<string> | null {
  const query = q.trim().toLowerCase();
  if (!query) return null;
  const byKey = new Map(nodes.map((n) => [n.key, n]));
  const visible = new Set<string>();
  for (const n of nodes) {
    if (!n.name.toLowerCase().includes(query)) continue;
    let cur: ScopeNode | undefined = n;
    while (cur && !visible.has(cur.key)) {
      visible.add(cur.key);
      cur = cur.parentKey ? byKey.get(cur.parentKey) : undefined;
    }
  }
  return visible;
}
