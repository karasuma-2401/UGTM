// Reproducible reference model, in metres only for internal consistency.
// Relative placement follows UIT's 2026 DGNL guide; dimensions are not surveyed.
// Architecture reference: UIT building guide, 2026; see docs/landing-page.md.
import { mkdir, writeFile } from 'node:fs/promises';
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';

globalThis.FileReader = class {
  readAsArrayBuffer(blob) {
    blob.arrayBuffer().then((value) => {
      this.result = value;
      this.onloadend?.();
    });
  }
};
const root = new THREE.Group();
root.name = 'UIT_reference_campus';
const materials = Object.fromEntries(
  Object.entries({
    stone: '#f0ede3',
    white: '#f8f7ee',
    blue: '#366b94',
    glass: '#7cb8ba',
    glassDark: '#497e8c',
    frame: '#dbe5dd',
    roof: '#bdcacc',
    paving: '#deded0',
    grass: '#a6b394',
    road: '#b4bbae',
    trunk: '#877663',
    leaf: '#527b61',
    leafLight: '#799571',
    orange: '#dc8253',
    water: '#94b9bb',
  }).map(([name, color]) => [
    name,
    new THREE.MeshStandardMaterial({
      color,
      roughness: name.startsWith('glass') ? 0.24 : 0.85,
      metalness: name.startsWith('glass') ? 0.25 : 0,
    }),
  ]),
);
let active = new THREE.Group();
root.add(active);
function mesh(geometry, material, x, y, z, rotation = 0) {
  const object = new THREE.Mesh(geometry, materials[material]);
  object.position.set(x, y, z);
  object.rotation.y = rotation;
  active.add(object);
  return object;
}
function box(x, y, z, w, h, d, material, rotation = 0) {
  return mesh(new THREE.BoxGeometry(w, h, d), material, x, y, z, rotation);
}
function cylinder(x, y, z, r, h, material, sides = 32) {
  return mesh(new THREE.CylinderGeometry(r, r, h, sides), material, x, y, z);
}
function building(name, fn) {
  active = new THREE.Group();
  active.name = name;
  root.add(active);
  fn();
  // Merge per material so thousands of façade elements remain a handful of draw calls.
  const buckets = new Map();
  for (const object of active.children) {
    object.updateMatrix();
    const geometry = object.geometry.clone().applyMatrix4(object.matrix);
    const bucket = buckets.get(object.material) ?? [];
    bucket.push(geometry);
    buckets.set(object.material, bucket);
    object.geometry.dispose();
  }
  active.clear();
  for (const [material, geometries] of buckets) {
    const geometry = mergeGeometries(geometries);
    geometry.computeBoundingSphere();
    active.add(new THREE.Mesh(geometry, material));
    geometries.forEach((g) => g.dispose());
  }
}

// Digitized centres in UIT's schematic, transformed into local site axes.
function plan(px, py) {
  const dx = px - 90,
    dy = py - 526;
  return [(dx * 0.809 + dy * 0.588 - 333) * 0.19, (315 - (dx * 0.588 - dy * 0.809)) * 0.19];
}
const planRotation = Math.atan2(0.588, 0.809);
function place(name, from, to, scale = 1) {
  const matrix = new THREE.Matrix4()
    .makeTranslation(to[0], 0, to[1])
    .multiply(new THREE.Matrix4().makeRotationY(planRotation))
    .multiply(new THREE.Matrix4().makeScale(scale, 1, scale))
    .multiply(new THREE.Matrix4().makeTranslation(-from[0], 0, -from[1]));
  root.getObjectByName(name).children.forEach((object) => object.geometry.applyMatrix4(matrix));
}
building('Landscape', () => {
  box(0, -1.9, 0, 128, 3, 126, 'stone');
  box(0, -0.3, 0, 127.8, 0.3, 125.8, 'grass');
  const road = (points, width = 4) => {
    for (let i = 1; i < points.length; i++) {
      const a = plan(...points[i - 1]),
        b = plan(...points[i]);
      const dx = b[0] - a[0],
        dz = b[1] - a[1];
      box(
        (a[0] + b[0]) / 2,
        0.05,
        (a[1] + b[1]) / 2,
        Math.hypot(dx, dz) + width,
        0.2,
        width,
        'road',
        -Math.atan2(dz, dx),
      );
    }
  };
  road(
    [
      [130, 535],
      [470, 55],
    ],
    8,
  );
  road([
    [300, 410],
    [350, 465],
    [430, 520],
    [685, 520],
    [710, 700],
    [650, 750],
    [530, 800],
    [370, 670],
    [350, 515],
  ]);
  road(
    [
      [350, 465],
      [405, 330],
      [530, 155],
    ],
    5,
  );
  road([
    [405, 330],
    [490, 410],
    [570, 455],
    [610, 480],
  ]);
  road(
    [
      [570, 155],
      [560, 340],
      [575, 445],
      [650, 445],
      [785, 460],
    ],
    3,
  );
  road(
    [
      [490, 510],
      [490, 596],
    ],
    3,
  );
  road(
    [
      [475, 575],
      [665, 575],
      [710, 615],
    ],
    3,
  );
  road([
    [710, 615],
    [760, 615],
    [805, 725],
  ]);
  const plaza = plan(697, 547);
  cylinder(plaza[0], 0.17, plaza[1], 12.3, 0.14, 'paving', 64);
  cylinder(plaza[0], 0.28, plaza[1], 6, 0.06, 'stone', 64);
  const gate = plan(295, 414);
  for (const offset of [-5, 5]) box(gate[0], 2, gate[1] + offset, 1.1, 4, 1.1, 'white');
  box(gate[0], 4.3, gate[1], 1.4, 0.7, 12, 'blue');
  const court = plan(597, 718);
  box(court[0], 0.1, court[1], 24, 0.18, 13, 'leafLight', planRotation);
});

building('E_glass_tower', () => {
  const x = -28,
    z = 13,
    w = 24,
    d = 12,
    h = 42;
  box(x, h / 2, z, w, h, d, 'glass');
  box(x, 1.8, z + 0.2, w + 1, 3.6, d + 0.6, 'blue');
  for (let level = 1; level <= 11; level++) {
    const y = level * 3.6;
    box(x, y, z + d / 2 + 0.05, w + 0.3, 0.24, 0.25, 'frame');
    box(x, y, z - d / 2 - 0.05, w + 0.3, 0.24, 0.25, 'frame');
    for (let col = 0; col < 12; col++) {
      const xx = x - w / 2 + 1 + col * 2;
      box(xx, y - 1.7, z + d / 2 + 0.06, 0.12, 3.4, 0.18, 'frame');
      box(
        xx,
        y - 2.7,
        z + d / 2 + 0.08,
        1.78,
        1.05,
        0.12,
        (level + col) % 4 === 0 ? 'glassDark' : 'glass',
      );
    }
    box(x + w / 2 + 0.05, y, z, 0.24, 0.24, d, 'frame');
  }
  // Rounded white circulation core visible in UIT's front façade.
  cylinder(x - 15, 21.5, z, 3.5, 43, 'white', 24);
  for (let i = 0; i < 11; i++) {
    box(x - 15, 3 + i * 3.6, z + 3.46, 1.8, 1.2, 0.18, 'blue');
    box(x + 14, 2.5 + i * 3.6, z, 3.5, 0.35, d + 0.5, 'white');
    box(x + 15.2, 3.8 + i * 3.6, z, 0.3, 2.4, d + 0.5, 'frame');
    box(x + 14, 3.5 + i * 3.6, z + (i % 2 ? 1.7 : -1.7), 2.7, 0.35, 7, 'stone').rotation.x =
      i % 2 ? -0.37 : 0.37;
  }
  box(x, 42.2, z, w + 1, 0.5, d + 1, 'white');
  box(x, 43, z, w - 2, 1, d - 2, 'roof');
  box(x, 4, z + 8, 16, 0.45, 5, 'white');
  for (const xx of [-34, -22]) box(xx, 2, z + 9.2, 0.4, 4, 0.4, 'frame');
});

building('A_teaching_block', () => {
  for (const [px, py, r] of [
    [492, 445, 11.5],
    [514, 350, 7.5],
  ]) {
    const [x, z] = plan(px, py);
    cylinder(x, 5.25, z, r, 10.5, 'stone', 8);
    for (let floor = 0; floor < 3; floor++) {
      cylinder(x, 3.4 + floor * 3.4, z, r + 0.5, 0.35, 'white', 8);
      for (let side = 0; side < 8; side++) {
        const a = (side * Math.PI) / 4;
        box(
          x + Math.sin(a) * (r * 0.925),
          1.5 + floor * 3.4,
          z + Math.cos(a) * (r * 0.925),
          r * 0.6,
          2.1,
          0.15,
          'glassDark',
          a,
        );
      }
    }
    cylinder(x, 10.8, z, r + 0.8, 0.5, 'roof', 8);
  }
  const [x, z] = plan(506, 392);
  box(x, 5.2, z, 5, 10.4, 9, 'stone', planRotation);
});

building('B_curved_courtyard', () => {
  // Faceted curved teaching wings: B's recognizable stepped amphitheatre shape.
  const centerX = 31,
    centerZ = -10,
    r = 24,
    sections = 13;
  for (let i = 0; i < sections; i++) {
    const a = Math.PI * (0.12 + (i / (sections - 1)) * 0.76);
    const x = centerX + Math.cos(a) * r,
      z = centerZ - Math.sin(a) * r;
    const angle = a - Math.PI / 2;
    const floors = i < 2 || i > 10 ? 5 : 6;
    box(x, floors * 1.65, z, 6.5, floors * 3.3, 10, 'white', angle);
    for (let floor = 0; floor < floors; floor++) {
      const inward = new THREE.Vector3(0, 0, 5.1).applyAxisAngle(new THREE.Vector3(0, 1, 0), angle);
      box(x + inward.x, 1.5 + floor * 3.3, z + inward.z, 5.4, 2.2, 0.12, 'glassDark', angle);
      box(x + inward.x, 3.1 + floor * 3.3, z + inward.z, 6.6, 0.3, 1.1, 'blue', angle);
    }
    box(x, floors * 3.3 + 0.2, z, 6.8, 0.4, 10.5, 'roof', angle);
  }
});

building('C_long_teaching_wing', () => {
  const x = -32,
    z = -36;
  box(x, 5.2, z, 47, 10.4, 10, 'white');
  for (let row = 0; row < 3; row++) {
    for (let col = 0; col < 15; col++) {
      box(x - 21.8 + col * 3.1, 1.6 + row * 3.4, z + 5.06, 2.1, 2.1, 0.12, 'glassDark');
      box(x - 23 + col * 3.2, 5.2, z + 6.2, 0.32, 10.4, 0.4, 'blue');
    }
    box(x, 3.3 + row * 3.4, z + 5.9, 48, 0.3, 2.2, 'white');
  }
  box(x, 10.8, z, 49, 0.5, 12, 'roof');
});

building('Student_services', () => {
  for (const [px, py, w, d] of [
    [762, 633, 12, 9],
    [679, 774, 13, 7],
  ]) {
    const [x, z] = plan(px, py);
    box(x, 2.5, z, w, 5, d, 'white', planRotation);
    box(x, 5.2, z, w + 1, 0.5, d + 1, 'blue', planRotation);
  }
});

building('Trees_and_paths', () => {
  let seed = 83;
  const rand = () => {
    seed = (seed * 16807) % 2147483647;
    return seed / 2147483647;
  };
  function tree(x, z, size = 1) {
    cylinder(x, 1.5 * size, z, 0.26 * size, 3 * size, 'trunk', 6);
    mesh(
      new THREE.IcosahedronGeometry(2.1 * size, 1),
      rand() > 0.5 ? 'leaf' : 'leafLight',
      x,
      4.2 * size,
      z,
    );
    mesh(
      new THREE.IcosahedronGeometry(1.8 * size, 1),
      'leafLight',
      x + size,
      3.7 * size,
      z + size * 0.5,
    );
  }
  for (let i = 0; i < 20; i++) {
    tree(-59, -54 + i * 5.7, 0.8 + rand() * 0.45);
    tree(59, -54 + i * 5.7, 0.8 + rand() * 0.45);
  }
  for (let i = 0; i < 18; i++) {
    tree(-55 + i * 6.4, -57, 0.8 + rand() * 0.3);
    tree(-55 + i * 6.4, 57, 0.8 + rand() * 0.3);
  }
  for (const [x, z] of [
    [-40, 5],
    [-40, -30],
    [-38, 38],
    [14, -46],
    [30, -45],
    [45, -40],
    [-23, 50],
    [-10, 47],
  ])
    tree(x, z);
});

place('E_glass_tower', [-28, 13], plan(420, 565), 0.72);
place('B_curved_courtyard', [31, -10], plan(697, 547), 0.72);
place('C_long_teaching_wing', [-32, -36], plan(545, 630));
const buffer = await new GLTFExporter().parseAsync(root, { binary: true, onlyVisible: true });
const out = new URL('../public/models/', import.meta.url);
await mkdir(out, { recursive: true });
await writeFile(new URL('uit-campus.glb', out), Buffer.from(buffer));
console.log(`UIT reference GLB: ${(buffer.byteLength / 1024).toFixed(0)} KB`);
