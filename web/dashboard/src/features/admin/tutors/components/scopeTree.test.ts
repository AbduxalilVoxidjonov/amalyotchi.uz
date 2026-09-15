import type { TutorScopeTree } from '../types';
import { computeNodeStates, flattenScopeTree, scopeKeysOf, visibleKeys } from './scopeTree';

const free = { tutorId: null, tutorName: null };
const other = { tutorId: 't9', tutorName: 'Boshqa Tyutor' };

/** f: d1(dir1: g1,g2 · dir2: g3) · d2 (boshqa tyutorniki: dir3: g4) · d3 (dir4: g5 — boshqa tyutorniki, g6). */
const TREE: TutorScopeTree = {
  id: 'f',
  name: 'Fakultet',
  code: 'F',
  ...free,
  departments: [
    {
      id: 'd1',
      name: 'Kafedra 1',
      ...free,
      directions: [
        {
          id: 'dir1',
          name: "Yo'nalish 1",
          ...free,
          groups: [
            { id: 'g1', name: '101', course: 1, students: 10, ...free },
            { id: 'g2', name: '102', course: 1, students: 12, ...free },
          ],
        },
        {
          id: 'dir2',
          name: "Yo'nalish 2",
          ...free,
          groups: [{ id: 'g3', name: '201', course: 2, students: 8, ...free }],
        },
      ],
    },
    {
      id: 'd2',
      name: 'Kafedra 2',
      ...other,
      directions: [
        {
          id: 'dir3',
          name: "Yo'nalish 3",
          ...free,
          groups: [{ id: 'g4', name: '301', course: 3, students: 5, ...free }],
        },
      ],
    },
    {
      id: 'd3',
      name: 'Kafedra 3',
      ...free,
      directions: [
        {
          id: 'dir4',
          name: "Yo'nalish 4",
          ...free,
          groups: [
            { id: 'g5', name: '401', course: 4, students: 7, ...other },
            { id: 'g6', name: '402', course: 4, students: 9, ...free },
          ],
        },
      ],
    },
  ],
};

const nodes = flattenScopeTree(TREE);
const kind = (states: ReturnType<typeof computeNodeStates>, key: string) => states.get(key)?.kind;

describe('flattenScopeTree', () => {
  it("chuqurlik-birinchi tartib, guruh/talaba yig'indisi", () => {
    expect(nodes.map((n) => n.key)).toEqual([
      'faculty:f',
      'department:d1',
      'direction:dir1',
      'group:g1',
      'group:g2',
      'direction:dir2',
      'group:g3',
      'department:d2',
      'direction:dir3',
      'group:g4',
      'department:d3',
      'direction:dir4',
      'group:g5',
      'group:g6',
    ]);
    const byKey = new Map(nodes.map((n) => [n.key, n]));
    expect(byKey.get('faculty:f')).toMatchObject({ groups: 6, students: 51, depth: 0 });
    expect(byKey.get('department:d1')).toMatchObject({ groups: 3, students: 30 });
    expect(byKey.get('direction:dir1')).toMatchObject({ groups: 2, students: 22 });
    expect(byKey.get('group:g1')).toMatchObject({ groups: 1, students: 10, course: 1, depth: 3 });
  });
});

describe('computeNodeStates', () => {
  it("boshqa tyutor tuguni: o'zi taken, avlodlari takenViaAncestor, ajdodlari takenInside", () => {
    const states = computeNodeStates(nodes, new Set(), 't1');
    expect(kind(states, 'department:d2')).toBe('taken');
    expect(kind(states, 'direction:dir3')).toBe('takenViaAncestor');
    expect(kind(states, 'group:g4')).toBe('takenViaAncestor');
    expect(kind(states, 'group:g5')).toBe('taken');
    expect(kind(states, 'direction:dir4')).toBe('takenInside');
    expect(kind(states, 'department:d3')).toBe('takenInside');
    expect(kind(states, 'faculty:f')).toBe('takenInside');
    expect(states.get('faculty:f')).toMatchObject({ tutorName: 'Boshqa Tyutor' });
    // Band tugun yonidagi erkin tugunlar.
    expect(kind(states, 'group:g6')).toBe('free');
    expect(kind(states, 'department:d1')).toBe('free');
  });

  it("tanlangan tugun avlodlari 'covered' (ota nomi bilan); joriy tyutorning o'z tuguni taken emas", () => {
    const own = flattenScopeTree({
      ...TREE,
      departments: TREE.departments.map((d) =>
        d.id === 'd1' ? { ...d, tutorId: 't1', tutorName: 'Men' } : d,
      ),
    });
    const states = computeNodeStates(own, new Set(['department:d1']), 't1');
    expect(kind(states, 'department:d1')).toBe('selected');
    expect(states.get('direction:dir1')).toEqual({ kind: 'covered', byName: 'Kafedra 1' });
    expect(states.get('group:g3')).toEqual({ kind: 'covered', byName: 'Kafedra 1' });
  });
});

describe('scopeKeysOf / visibleKeys', () => {
  it("detail.scopes → 'level:id' kalitlar", () => {
    const keys = scopeKeysOf([
      {
        id: 's1',
        level: 'direction',
        facultyId: 'f',
        departmentId: 'd1',
        directionId: 'dir1',
        groupId: null,
        name: '',
        path: '',
        groups: 2,
        students: 22,
      },
      {
        id: 's2',
        level: 'faculty',
        facultyId: 'f',
        departmentId: null,
        directionId: null,
        groupId: null,
        name: '',
        path: '',
        groups: 6,
        students: 51,
      },
    ]);
    expect([...keys]).toEqual(['direction:dir1', 'faculty:f']);
  });

  it("qidiruv: mos tugunlar + ajdodlari; bo'sh so'rov — null", () => {
    expect(visibleKeys(nodes, '  ')).toBeNull();
    expect([...visibleKeys(nodes, '40')!]).toEqual([
      'group:g5',
      'direction:dir4',
      'department:d3',
      'faculty:f',
      'group:g6',
    ]);
    expect(visibleKeys(nodes, "yo'q")!.size).toBe(0);
  });
});
