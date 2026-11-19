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
    uitBlue: '#61a9ce',
    skyBlue: '#a0ccdd',
    silver: '#b5c1c2',
    sandstone: '#c9b28e',
    gold: '#d1bb7b',
    courtBlue: '#5a8db5',
    courtGreen: '#73a58e',
    railRed: '#b96259',
    shadeYellow: '#d9b967',
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
function beam(a, b, width, material, depth = width) {
  const start = new THREE.Vector3(...a),
    end = new THREE.Vector3(...b);
  const object = box(
    ...start.clone().add(end).multiplyScalar(0.5).toArray(),
    width,
    start.distanceTo(end),
    depth,
    material,
  );
  object.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), end.sub(start).normalize());
  return object;
}
function steps(x, z, width, count = 5, rise = 0.18, rotation = 0) {
  for (let i = 0; i < count; i++) {
    const offset = new THREE.Vector3(0, 0, -i * 0.32).applyAxisAngle(
      new THREE.Vector3(0, 1, 0),
      rotation,
    );
    box(
      x + offset.x,
      ((i + 1) * rise) / 2,
      z + offset.z,
      width,
      (i + 1) * rise,
      0.8,
      'stone',
      rotation,
    );
  }
}
function bench(x, z, rotation = 0) {
  const local = (xx, yy, zz, w, h, d, mat) => {
    const offset = new THREE.Vector3(xx, yy, zz).applyAxisAngle(
      new THREE.Vector3(0, 1, 0),
      rotation,
    );
    box(x + offset.x, offset.y, z + offset.z, w, h, d, mat, rotation);
  };
  local(0, 0.55, 0, 2.6, 0.18, 0.7, 'white');
  local(0, 0.95, -0.3, 2.6, 0.65, 0.15, 'white');
  for (const xx of [-0.85, 0.85]) local(xx, 0.23, 0, 0.22, 0.46, 0.6, 'stone');
}
function planter(x, z, w = 3, d = 2, rotation = 0) {
  box(x, 0.22, z, w, 0.44, d, 'white', rotation);
  box(x, 0.49, z, w - 0.2, 0.16, d - 0.2, 'leaf', rotation);
  for (let i = -1; i <= 1; i++) {
    const offset = new THREE.Vector3((i * w) / 4, 0, 0).applyAxisAngle(
      new THREE.Vector3(0, 1, 0),
      rotation,
    );
    mesh(new THREE.IcosahedronGeometry(0.6, 0), 'leafLight', x + offset.x, 0.85, z + offset.z);
  }
}
function canopy(a, b, width = 2.2, height = 3.6) {
  const dx = b[0] - a[0],
    dz = b[1] - a[1],
    length = Math.hypot(dx, dz),
    rotation = -Math.atan2(dz, dx);
  box(
    (a[0] + b[0]) / 2,
    height,
    (a[1] + b[1]) / 2,
    length + 0.6,
    0.17,
    width + 0.4,
    'roof',
    rotation,
  );
  for (let i = 0; i <= Math.ceil(length / 5); i++) {
    const t = i / Math.ceil(length / 5),
      x = a[0] + dx * t,
      z = a[1] + dz * t;
    for (const side of [-1, 1]) {
      const xx = x + (((side * dz) / length) * width) / 2,
        zz = z - (((side * dx) / length) * width) / 2;
      cylinder(xx, height / 2, zz, 0.065, height, 'uitBlue', 6);
    }
    box(x, height - 0.15, z, 0.1, 0.12, width + 0.4, 'uitBlue', rotation);
  }
}
function railing(a, b, height = 1.1, material = 'silver') {
  for (const y of [0.4, height]) beam([a[0], y, a[1]], [b[0], y, b[1]], 0.055, material);
  const length = Math.hypot(b[0] - a[0], b[1] - a[1]);
  for (let i = 0; i <= Math.ceil(length / 2); i++) {
    const t = i / Math.ceil(length / 2);
    cylinder(
      a[0] + (b[0] - a[0]) * t,
      height / 2,
      a[1] + (b[1] - a[1]) * t,
      0.045,
      height,
      material,
      6,
    );
  }
}
function pitchedRoof(x, z, w, d, y, material = 'roof', rotation = 0) {
  for (const side of [-1, 1]) {
    const offset = new THREE.Vector3(0, 0, (side * d) / 4).applyAxisAngle(
      new THREE.Vector3(0, 1, 0),
      rotation,
    );
    const roof = box(x + offset.x, y, z + offset.z, w, 0.16, d / 2 + 0.2, material, rotation);
    roof.rotateX(side * 0.18);
  }
}
// Simple architectural stroke lettering, exported as geometry with no font/texture dependency.
const glyphs = {
  U: [
    [
      [0, 1],
      [0, 0.2],
      [0.2, 0],
      [0.8, 0],
      [1, 0.2],
      [1, 1],
    ],
  ],
  I: [
    [
      [0, 1],
      [1, 1],
    ],
    [
      [0.5, 1],
      [0.5, 0],
    ],
    [
      [0, 0],
      [1, 0],
    ],
  ],
  T: [
    [
      [0, 1],
      [1, 1],
    ],
    [
      [0.5, 1],
      [0.5, 0],
    ],
  ],
  A: [
    [
      [0, 0],
      [0.5, 1],
      [1, 0],
    ],
    [
      [0.2, 0.4],
      [0.8, 0.4],
    ],
  ],
  B: [
    [
      [0, 0],
      [0, 1],
      [0.7, 1],
      [1, 0.8],
      [0.7, 0.55],
      [0, 0.55],
    ],
    [
      [0.7, 0.55],
      [1, 0.25],
      [0.7, 0],
      [0, 0],
    ],
  ],
  C: [
    [
      [1, 0.9],
      [0.7, 1],
      [0.15, 1],
      [0, 0.8],
      [0, 0.2],
      [0.15, 0],
      [0.7, 0],
      [1, 0.1],
    ],
  ],
  E: [
    [
      [1, 1],
      [0, 1],
      [0, 0],
      [1, 0],
    ],
    [
      [0, 0.5],
      [0.8, 0.5],
    ],
  ],
  F: [
    [
      [1, 1],
      [0, 1],
      [0, 0],
    ],
    [
      [0, 0.5],
      [0.8, 0.5],
    ],
  ],
  G: [
    [
      [1, 0.85],
      [0.75, 1],
      [0.2, 1],
      [0, 0.8],
      [0, 0.2],
      [0.2, 0],
      [1, 0],
      [1, 0.45],
      [0.6, 0.45],
    ],
  ],
  H: [
    [
      [0, 0],
      [0, 1],
    ],
    [
      [1, 0],
      [1, 1],
    ],
    [
      [0, 0.5],
      [1, 0.5],
    ],
  ],
  N: [
    [
      [0, 0],
      [0, 1],
      [1, 0],
      [1, 1],
    ],
  ],
  O: [
    [
      [0.2, 0],
      [0, 0.2],
      [0, 0.8],
      [0.2, 1],
      [0.8, 1],
      [1, 0.8],
      [1, 0.2],
      [0.8, 0],
      [0.2, 0],
    ],
  ],
  R: [
    [
      [0, 0],
      [0, 1],
      [0.8, 1],
      [1, 0.8],
      [1, 0.6],
      [0.8, 0.5],
      [0, 0.5],
    ],
    [
      [0.5, 0.5],
      [1, 0],
    ],
  ],
  S: [
    [
      [1, 0.9],
      [0.8, 1],
      [0.2, 1],
      [0, 0.8],
      [0, 0.6],
      [1, 0.4],
      [1, 0.2],
      [0.8, 0],
      [0.2, 0],
      [0, 0.1],
    ],
  ],
  V: [
    [
      [0, 1],
      [0.5, 0],
      [1, 1],
    ],
  ],
  D: [
    [
      [0, 0],
      [0, 1],
      [0.7, 1],
      [1, 0.75],
      [1, 0.25],
      [0.7, 0],
      [0, 0],
    ],
  ],
  M: [
    [
      [0, 0],
      [0, 1],
      [0.5, 0.4],
      [1, 1],
      [1, 0],
    ],
  ],
  L: [
    [
      [0, 1],
      [0, 0],
      [1, 0],
    ],
  ],
  Y: [
    [
      [0, 1],
      [0.5, 0.5],
      [1, 1],
    ],
    [
      [0.5, 0.5],
      [0.5, 0],
    ],
  ],
  P: [
    [
      [0, 0],
      [0, 1],
      [0.8, 1],
      [1, 0.8],
      [1, 0.6],
      [0.8, 0.5],
      [0, 0.5],
    ],
  ],
};
function lettering(text, x, y, z, height, material = 'blue', rotation = 0) {
  const width = text.length * height * 0.8;
  for (let i = 0; i < text.length; i++)
    for (const path of glyphs[text[i]] ?? []) {
      const points = path.map(([xx, yy]) =>
        new THREE.Vector3((i * 0.8 + xx * 0.58) * height - width / 2, yy * height, 0)
          .applyAxisAngle(new THREE.Vector3(0, 1, 0), rotation)
          .add(new THREE.Vector3(x, y, z))
          .toArray(),
      );
      for (let j = 1; j < points.length; j++)
        beam(points[j - 1], points[j], height * 0.055, material, height * 0.07);
    }
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
    // End inside gate B; the old endpoint projected beyond the campus island.
    [775, 690],
  ]);
  const plaza = plan(697, 547);
  cylinder(plaza[0], 0.17, plaza[1], 12.3, 0.14, 'paving', 64);
  cylinder(plaza[0], 0.28, plaza[1], 6, 0.06, 'stone', 64);
  box(-48, 0.03, 20, 30, 0.12, 7, 'road');
});

building('E_glass_tower', () => {
  const x = -28,
    z = 13,
    w = 24,
    d = 12,
    h = 42;
  box(x, h / 2, z, w, h, d, 'glass');
  box(x, 1.8, z + 0.2, w + 1, 3.6, d + 0.6, 'stone');
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
  for (const xx of [-34, -22]) cylinder(xx, 2, z + 9.2, 0.38, 4, 'silver', 12);
  steps(x, z + 11.5, 9, 10, 0.17);
  for (const side of [-1, 1]) {
    lettering(
      'TRUONG DAI HOC CONG NGHE THONG TIN',
      x,
      43.6,
      z + side * 6.6,
      0.85,
      'blue',
      side === 1 ? 0 : Math.PI,
    );
    for (const xx of [-35, -21]) box(xx, 43.3, z + side * 6.4, 0.1, 1.5, 0.1, 'silver');
  }
  for (let i = 0; i < 6; i++) box(x - 5 + i * 2, 1.8, z + 6.37, 1.65, 3.1, 0.12, 'glassDark');
});

building('A_teaching_block', () => {
  for (const [px, py, r] of [
    [492, 445, 11.5],
    [514, 350, 7.5],
  ]) {
    const [x, z] = plan(px, py);
    cylinder(x, 5.25, z, r, 10.5, 'skyBlue', 8);
    cylinder(x, 0.35, z, r + 0.1, 0.7, 'stone', 8);
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
          'silver',
          a,
        );
        for (let slat = 0; slat < 8; slat++)
          box(
            x + Math.sin(a) * (r * 0.925 + 0.1),
            0.55 + floor * 3.4 + slat * 0.27,
            z + Math.cos(a) * (r * 0.925 + 0.1),
            r * 0.62,
            0.075,
            0.14,
            'white',
            a,
          );
      }
      cylinder(x, 3.15 + floor * 3.4, z, r + 0.2, 0.35, 'uitBlue', 8);
    }
    for (let side = 0; side < 8; side += 2) {
      const a = (side * Math.PI) / 4;
      cylinder(x + Math.sin(a) * r, 5.3, z + Math.cos(a) * r, 0.23, 10.6, 'silver', 8);
    }
    box(x - r, 1.5, z, 0.2, 2.7, r * 0.8, 'glassDark');
    box(x - r - 1, 3.05, z, 2.8, 0.28, r * 0.85, 'silver');
    steps(x - r - 2.4, z, r * 0.8, 5, 0.16, -Math.PI / 2);
    cylinder(x, 10.8, z, r + 0.8, 0.5, 'roof', 8);
  }
  const [x, z] = plan(506, 392);
  box(x, 5.2, z, 5, 10.4, 9, 'skyBlue', planRotation);
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
    const normal = new THREE.Vector3(0, 0, 1).applyAxisAngle(new THREE.Vector3(0, 1, 0), angle);
    // Recess the solid classrooms; the inner ring is a genuinely open gallery.
    box(
      x - normal.x * 1.2,
      floors * 1.65,
      z - normal.z * 1.2,
      6.5,
      floors * 3.3,
      7.2,
      'skyBlue',
      angle,
    );
    box(x, 0.3, z, 6.5, 0.6, 10, 'courtGreen', angle);
    for (let floor = 0; floor < floors; floor++) {
      const inward = new THREE.Vector3(0, 0, 5.1).applyAxisAngle(new THREE.Vector3(0, 1, 0), angle);
      box(x, 0.16 + floor * 3.3, z, 6.6, 0.3, 10.4, 'white', angle);
      box(
        x + normal.x * 2.48,
        1.5 + floor * 3.3,
        z + normal.z * 2.48,
        2.8,
        2.4,
        0.12,
        'glassDark',
        angle,
      );
      for (const xx of [-2.9, 2.9]) {
        const offset = new THREE.Vector3(xx, 0, 5).applyAxisAngle(
          new THREE.Vector3(0, 1, 0),
          angle,
        );
        box(x + offset.x, 1.65 + floor * 3.3, z + offset.z, 0.25, 3.3, 0.3, 'white', angle);
      }
      for (const y of [0.55, 1.05])
        box(x + inward.x, y + floor * 3.3, z + inward.z, 6.2, 0.065, 0.07, 'silver', angle);
      for (let rail = -2.7; rail <= 2.8; rail += 0.9) {
        const offset = new THREE.Vector3(rail, 0, 5.1).applyAxisAngle(
          new THREE.Vector3(0, 1, 0),
          angle,
        );
        box(x + offset.x, 0.65 + floor * 3.3, z + offset.z, 0.045, 1.1, 0.055, 'silver', angle);
      }
      box(x + inward.x, 3.1 + floor * 3.3, z + inward.z, 6.6, 0.22, 0.6, 'uitBlue', angle);
      const back = new THREE.Vector3(0, 0, -4.85).applyAxisAngle(new THREE.Vector3(0, 1, 0), angle);
      box(x + back.x, 1.7 + floor * 3.3, z + back.z, 3.4, 1.6, 0.12, 'white', angle);
      box(
        x + back.x * 1.01,
        1.7 + floor * 3.3,
        z + back.z * 1.01,
        2.95,
        1.3,
        0.12,
        'glassDark',
        angle,
      );
    }
    if ([2, 6, 10].includes(i)) {
      box(
        x + normal.x * 4.5,
        floors * 1.65,
        z + normal.z * 4.5,
        1.4,
        floors * 3.3,
        1.25,
        'white',
        angle,
      );
      box(
        x + normal.x * 5.15,
        floors * 1.65,
        z + normal.z * 5.15,
        0.85,
        floors * 3.3 - 0.6,
        0.12,
        'glass',
        angle,
      );
      for (let floor = 1; floor < floors; floor++)
        box(x + normal.x * 5.22, floor * 3.3, z + normal.z * 5.22, 1, 0.12, 0.08, 'white', angle);
    }
    box(x, floors * 3.3 + 0.2, z, 6.8, 0.4, 10.5, 'roof', angle);
  }
});

building('C_long_teaching_wing', () => {
  const x = -32,
    z = -36;
  // Open central atrium with two teaching wings, rather than hiding the pond in a solid box.
  for (const side of [-1, 1]) {
    box(x + side * 14.5, 5.2, z, 18, 10.4, 10, 'skyBlue');
    box(x + side * 14.5, 10.8, z, 19, 0.5, 12, 'roof');
  }
  box(x, 5.2, z - 4.3, 11, 10.4, 1.4, 'skyBlue');
  for (let row = 0; row < 3; row++) {
    for (let col = 0; col < 15; col++) {
      if (col >= 6 && col <= 8) continue;
      box(x - 21.8 + col * 3.1, 1.6 + row * 3.4, z + 5.06, 2.1, 2.1, 0.12, 'glassDark');
      box(x - 23 + col * 3.2, 5.2, z + 6.2, 0.32, 10.4, 0.4, 'blue');
    }
    for (const side of [-1, 1])
      box(x + side * 14.5, 3.3 + row * 3.4, z + 5.9, 19, 0.3, 2.2, 'white');
    box(x, 3.3 + row * 3.4, z - 3.6, 11, 0.25, 2, 'white');
    for (const side of [-1, 1])
      box(x + side * 14.5, 1.6 + row * 3.4, z - 5.05, 16, 1.9, 0.15, 'glassDark');
  }
  box(x, 4.1, z + 6.1, 12, 0.45, 3.5, 'white');
  for (const side of [-1, 1]) box(x + side * 5.1, 2, z + 7, 0.5, 4, 0.5, 'uitBlue');
  steps(x, z + 8.4, 11, 5, 0.16);
  const rim = cylinder(x, 0.26, z, 2.6, 0.5, 'silver', 32);
  rim.scale.set(1.75, 1, 0.9);
  const water = cylinder(x, 0.53, z, 2.35, 0.06, 'water', 32);
  water.scale.set(1.75, 1, 0.9);
  for (const [xx, zz, r] of [
    [0, 0, 1.1],
    [-1, 0.4, 0.7],
    [0.8, -0.5, 0.6],
  ]) {
    mesh(new THREE.ConeGeometry(r, r * 2.4, 5), 'stone', x + xx, 0.5 + r, z + zz);
    mesh(new THREE.IcosahedronGeometry(r * 0.7, 0), 'leaf', x + xx, 1.1 + r, z + zz);
  }
});

building('Student_services', () => {
  box(55, 2.5, 25, 12, 5, 9, 'skyBlue');
  box(55, 5.2, 25, 13, 0.4, 10, 'white');
  for (let i = 0; i < 5; i++) box(50.5 + i * 2.2, 2.7, 29.6, 1.5, 2.6, 0.12, 'glassDark');
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
    const z = -54 + i * 5.7;
    if (Math.abs(z - 20) > 10) tree(-59, z, 0.8 + rand() * 0.45);
    if (Math.abs(z - 7) > 10) tree(59, z, 0.8 + rand() * 0.45);
  }
  for (let i = 0; i < 18; i++) {
    const x = -55 + i * 6.4;
    tree(x, -57, 0.8 + rand() * 0.3);
    tree(x, 57, 0.8 + rand() * 0.3);
  }
  for (const [x, z] of [
    [-40, 5],
    [-38, 38],
    [4, -50],
    [18, -39],
    [-23, 50],
    [-10, 47],
  ])
    tree(x, z);
  for (const [x, z] of [
    [-48, 4],
    [-37, -7],
    [-34, 30],
    [-41, 43],
    [-22, -34],
    [2, -35],
    [6, -46],
    [43, 46],
    [49, 33],
    [-8, 47],
    [56, 40],
    [39, 7],
    [17, -29],
  ])
    tree(x, z, 0.9 + rand() * 0.4);
});

building('Gate_A_and_UIT_landmark', () => {
  for (const z of [10, 30]) {
    box(-61, 1.7, z, 1.1, 3.4, 1.1, 'sandstone');
    box(-61, 3.45, z, 1.35, 0.2, 1.35, 'white');
  }
  // Folded sliding leaves leave the main entrance open, as in the reference photograph.
  for (const start of [10.6, 24]) {
    for (let i = 0; i < 7; i++) {
      const z = start + i * 0.8;
      beam([-61, 0.3, z], [-61, 1.8, z + 0.8], 0.065, 'silver');
      beam([-61, 1.8, z], [-61, 0.3, z + 0.8], 0.065, 'silver');
      cylinder(-61, 0.95, z, 0.035, 1.7, 'silver', 6);
    }
  }
  box(-61.2, 1.2, 36, 1.3, 2.4, 9, 'sandstone');
  lettering('UIT', -61.9, 0.65, 36, 1.1, 'blue', -Math.PI / 2);
  for (const range of [
    [-55, 9],
    [41, 54],
  ])
    for (let z = range[0]; z < range[1]; z += 1.1) {
      box(-61.7, 0.85, z, 0.12, 1.7, 0.12, 'white');
      box(-61.7, 1.2, z + 0.5, 0.12, 0.1, 1.15, 'white');
    }
  box(-56, 1.5, 31, 4.5, 3, 4, 'white');
  pitchedRoof(-56, 31, 5.1, 4.6, 3.25, 'roof');
  box(-58.3, 1.9, 31, 0.1, 1, 2.6, 'glass');
  box(-24.5, 0.28, 29, 2.1, 0.56, 14, 'stone');
  lettering('UIT', -24.6, 1.0, 29, 4.2, 'gold', -Math.PI / 2);
  for (const [index, z] of [23.2, 27.2, 31.2, 35.2].entries()) {
    cylinder(-21.4, 4.7, z, 0.055, 9.4, 'silver', 8);
    box(-21.4, 8.1, z + 0.9, 0.04, 1.15, 1.8, index === 1 ? 'railRed' : 'white');
  }
  for (const z of [19.6, 38.5]) planter(-25.5, z, 2.4, 2.4);
});

building('Covered_walkways_and_event_square', () => {
  box(12, 0.1, 10, 19, 0.18, 11, 'paving');
  for (let x = 3; x <= 21; x += 2) box(x, 0.205, 10, 0.025, 0.012, 11, 'stone');
  for (let z = 5; z <= 15; z += 2) box(12, 0.205, z, 19, 0.012, 0.025, 'stone');
  const routes = [
    [
      // Include the 0.3 m roof overhang so the end aligns with E's left glass edge.
      [-17.4, 16.8],
      [7, 16.8],
    ],
    [
      [7, 16.8],
      [10, 7],
    ],
    [
      [10, 7],
      [24, 7],
    ],
    [
      [24, 7],
      [39, 0],
    ],
    [
      [39, 0],
      [44, 9],
    ],
    [
      [44, 9],
      [51, 17],
    ],
    [
      [-30, -1],
      [-40, -21],
    ],
    [
      [-40, -21],
      [-41, -34],
    ],
  ];
  for (const [a, b] of routes) canopy(a, b);
  for (const [x, z, color] of [
    [5, 10, 'shadeYellow'],
    [16, 12, 'railRed'],
    [22, 9, 'shadeYellow'],
  ]) {
    for (let i = -6; i <= 6; i++) {
      const dz = i * 0.36,
        half = Math.sqrt(Math.max(0, 2.5 ** 2 - dz ** 2)) * 1.2;
      box(x, 4.15, z + dz, half * 2, 0.11, 0.11, color);
    }
    for (const [dx, dz] of [
      [-2, -1],
      [2, -1],
      [0, 2],
    ])
      cylinder(x + dx, 2.05, z + dz, 0.07, 4.1, color, 6);
  }
  for (const [x, z, r] of [
    [3, 7, Math.PI / 2],
    [3, 13, Math.PI / 2],
    [11, 4, 0],
    [19, 4, 0],
    [24, 12, -Math.PI / 2],
    [-30, -4, -Math.PI / 2],
    [-32, -12, -Math.PI / 2],
  ])
    bench(x, z, r);
  for (const [x, z, w, d] of [
    [4, 16, 3, 1.2],
    [13, 16, 5, 1.2],
    [22, 16, 3, 1.2],
    [-30, 4, 2, 4],
    [-32, -17, 2, 3],
  ])
    planter(x, z, w, d);
  steps(32, -13, 16, 7, 0.13);
  planter(21, -12, 2, 4);
  planter(43, -8, 2, 4);
  bench(25, -9, Math.PI / 2);
  bench(40, -7, -Math.PI / 2);
});

building('Palms_and_garden_furniture', () => {
  for (const [x, z] of [
    [-23, 22],
    [-23, 38],
    [-29, 25],
    [-29, 34],
    [-42, 7],
    [-38, 35],
  ]) {
    cylinder(x, 2.2, z, 0.17, 4.4, 'trunk', 7);
    for (let i = 0; i < 7; i++) {
      const a = (i * Math.PI * 2) / 7;
      const leaf = mesh(
        new THREE.ConeGeometry(0.55, 3, 4),
        'leaf',
        x + Math.cos(a) * 1.1,
        4.5,
        z + Math.sin(a) * 1.1,
      );
      leaf.quaternion.setFromUnitVectors(
        new THREE.Vector3(0, 1, 0),
        new THREE.Vector3(Math.cos(a), -0.25, Math.sin(a)).normalize(),
      );
    }
    cylinder(x, 0.28, z, 0.9, 0.5, 'white', 12);
  }
  for (const [x, z] of [
    [-48, 10],
    [-48, -2],
    [-37, -24],
    [-30, 42],
    [9, -35],
    [5, -46],
    [38, 48],
    [52, 40],
  ]) {
    bench(x + 2, z, Math.PI / 2);
    planter(x, z, 2, 2);
  }
  for (const [x, z] of [
    [-52, 22],
    [-39, 19],
    [-31, -26],
    [1, 20],
    [25, 16],
    [44, 17],
    [48, -32],
    [15, 46],
  ]) {
    cylinder(x, 2.7, z, 0.055, 5.4, 'silver', 6);
    cylinder(x, 5.45, z, 0.38, 0.16, 'white', 10);
  }
});

building('Sports_annex_ground', () => {
  // User-marked area is beyond the existing rear tree line, beside the low dining
  // house. Add a joined island extension instead of occupying its interior lawn.
  box(32, -1.9, -82, 58, 3, 38, 'stone');
  box(32, -0.3, -82, 57.8, 0.3, 37.8, 'grass');
  box(18.5, 0.05, -54.5, 3, 0.2, 33, 'paving');
  box(32, 0.05, -67, 54, 0.2, 3, 'paving');
  railing([4, -100], [60, -100]);
  railing([4, -100], [4, -64]);
  railing([60, -100], [60, -64]);
  for (const [x, z] of [
    [6, -68],
    [58, -96],
  ]) {
    cylinder(x, 2.7, z, 0.055, 5.4, 'silver', 6);
    cylinder(x, 5.45, z, 0.38, 0.16, 'white', 10);
  }
});

building('Sports_courts', () => {
  // Move the courts and every associated prop together onto the annex. The old
  // rear boundary (z = -63) remains an internal tree-lined divider.
  active.position.z = -37;
  function court(x, z, w, d, basketball = false) {
    box(x, 0.12, z, w + 1, 0.24, d + 1, 'courtGreen');
    box(x, 0.25, z, w, 0.04, d, 'courtBlue');
    for (const side of [-1, 1]) {
      box(x + (side * w) / 2, 0.28, z, 0.075, 0.025, d, 'white');
      box(x, 0.28, z + (side * d) / 2, w, 0.025, 0.075, 'white');
    }
    box(x, 0.28, z, w, 0.025, 0.07, 'white');
    if (basketball) {
      for (let i = 0; i < 32; i++) {
        const a = (i * Math.PI) / 16,
          b = ((i + 1) * Math.PI) / 16;
        beam(
          [x + Math.cos(a) * 2, 0.28, z + Math.sin(a) * 2],
          [x + Math.cos(b) * 2, 0.28, z + Math.sin(b) * 2],
          0.065,
          'white',
          0.025,
        );
      }
      for (const side of [-1, 1]) {
        box(x, 0.285, z + side * (d / 2 - 2.2), 5, 0.03, 4.3, 'orange');
        cylinder(x, 1.65, z + side * (d / 2 - 0.3), 0.07, 3.3, 'silver', 7);
        box(x, 3.3, z + side * (d / 2 - 1), 1.9, 1.1, 0.12, 'white');
        const hoop = mesh(
          new THREE.TorusGeometry(0.35, 0.035, 4, 12),
          'railRed',
          x,
          2.95,
          z + side * (d / 2 - 1.4),
        );
        hoop.rotation.x = Math.PI / 2;
      }
    } else {
      for (const xx of [-w / 2, w / 2]) cylinder(x + xx, 0.9, z, 0.045, 1.8, 'uitBlue', 6);
      for (const y of [0.6, 1, 1.4]) beam([x - w / 2, y, z], [x + w / 2, y, z], 0.025, 'white');
      for (let xx = -w / 2; xx <= w / 2; xx += 0.5)
        beam([x + xx, 0.6, z], [x + xx, 1.4, z], 0.015, 'silver');
      for (const side of [-1, 1]) box(x, 0.29, z + side * d * 0.22, w, 0.025, 0.045, 'white');
      box(x, 0.29, z, 0.045, 0.025, d, 'white');
    }
  }
  // Local layout of the cluster, before the annex translation above.
  court(49.5, -44, 13, 20, true);
  court(31, -55, 12, 7);
  court(14.5, -50, 12, 7);
  const fence = [
    [42, -55],
    [57, -55],
    [57, -33],
    [42, -33],
    [42, -55],
  ];
  for (let i = 1; i < fence.length; i++) {
    const a = fence[i - 1],
      b = fence[i];
    railing(a, b, 4.5);
    for (const y of [1, 2, 3, 4]) beam([a[0], y, a[1]], [b[0], y, b[1]], 0.025, 'silver');
    const n = Math.ceil(Math.hypot(b[0] - a[0], b[1] - a[1]) / 0.7);
    for (let j = 0; j <= n; j++)
      beam(
        [a[0] + ((b[0] - a[0]) * j) / n, 0.2, a[1] + ((b[1] - a[1]) * j) / n],
        [a[0] + ((b[0] - a[0]) * j) / n, 4.5, a[1] + ((b[1] - a[1]) * j) / n],
        0.018,
        'silver',
      );
  }
  railing([8, -45], [21, -45], 1, 'railRed');
  for (const x of [10, 16, 20]) bench(x, -44);
  cylinder(21.5, 1, -58, 0.35, 2, 'silver', 12);
  cylinder(21.5, 2.6, -58, 0.075, 1.3, 'silver', 8);
  const waterShade = cylinder(21.5, 3.3, -58, 1.6, 0.12, 'roof', 20);
  waterShade.scale.z = 0.7;
});

building('Parking_shelters', () => {
  for (const [x, z, w, d] of [
    [-43, -40, 28, 11],
    [-43, -17, 24, 9],
  ]) {
    box(x, 0.02, z, w, 0.1, d, 'road');
    pitchedRoof(x, z, w + 1, d + 1, 3.7);
    for (let xx = -w / 2; xx <= w / 2; xx += w / 4)
      for (const side of [-1, 1])
        cylinder(x + xx, 1.8, z + (side * d) / 2, 0.08, 3.6, 'uitBlue', 6);
    for (let i = 0; i < 10; i++) {
      const xx = x - w / 2 + 1.5 + (i * (w - 3)) / 9;
      box(xx, 0.09, z, 0.05, 0.02, d - 1, 'white');
      for (const side of [-1, 1]) {
        cylinder(xx, 0.45, z + side * 2, 0.23, 0.12, 'glassDark', 8).rotation.z = Math.PI / 2;
        cylinder(xx, 0.45, z + side * 3.2, 0.23, 0.12, 'glassDark', 8).rotation.z = Math.PI / 2;
        box(xx, 0.72, z + side * 2.6, 0.48, 0.45, 1.5, i % 3 ? 'silver' : 'railRed');
        box(xx, 0.98, z + side * 2.6, 0.4, 0.12, 0.8, 'glassDark');
      }
    }
  }
  box(-30, 0.98, -29, 2.6, 1.9, 8, 'white');
  box(-30, 2, -29, 2.45, 0.7, 7.4, 'glass');
  for (const side of [-1, 1])
    for (const z of [-31.5, -26.5])
      cylinder(-30 + side * 1.25, 0.45, z, 0.45, 0.2, 'glassDark', 10).rotation.z = Math.PI / 2;
});

building('Canteen_and_student_dining', () => {
  for (const [x, z, w, d, dining] of [
    [31, -44, 18, 10, true],
    [51, -23, 14, 11, false],
  ]) {
    box(x, 2.4, z, w, 4.8, d, 'skyBlue');
    pitchedRoof(x, z, w + 1, d + 1, 5.1, dining ? 'roof' : 'white');
    box(x, 3.6, z + d / 2 + 0.8, w, 0.22, 2, 'white');
    for (let i = 0; i < 6; i++)
      box(
        x - w / 2 + 1.4 + (i * (w - 2.8)) / 5,
        2,
        z + d / 2 + 0.04,
        (w - 3) / 6,
        2.9,
        0.12,
        'glass',
      );
    lettering(dining ? 'NHA AN' : 'CANTEEN', x, 3.9, z + d / 2 + 0.1, 0.55, 'blue');
    for (const side of [-1, 1]) planter(x + side * (w / 2 - 0.9), z + d / 2 + 1.7, 1.6, 1.6);
    for (const xx of [-w / 3, 0, w / 3]) {
      box(x + xx, 0.85, z + d / 2 + 3, 2, 0.15, 1, 'white');
      for (const side of [-1, 1])
        box(x + xx, 0.48, z + d / 2 + 3 + side * 0.9, 2, 0.16, 0.35, 'stone');
    }
  }
  canopy([43, -15], [58, -15], 2.3, 3.3);
  railing([22, -37], [40, -37], 1.1);
});

building('Gate_B', () => {
  for (const z of [1, 13]) {
    box(61, 1.8, z, 1.15, 3.6, 1.15, 'sandstone');
    cylinder(61, 3.75, z, 0.3, 0.2, 'white', 8);
  }
  for (const start of [1.6, 8])
    for (let i = 0; i < 5; i++) cylinder(61, 1.3, start + i * 0.85, 0.035, 2.6, 'gold', 6);
  for (const y of [0.4, 2.1])
    for (const [a, b] of [
      [1.6, 5],
      [8, 12],
    ])
      beam([61, y, a], [61, y, b], 0.07, 'gold');
  box(61, 1.15, -4.2, 1.1, 2.3, 8, 'sandstone');
  lettering('CONG B', 61.6, 0.7, -4.2, 0.8, 'blue', Math.PI / 2);
  box(57, 1.4, 16, 4, 2.8, 3.5, 'white');
  box(57, 2.95, 16, 4.6, 0.25, 4.1, 'roof');
  box(59.05, 1.8, 16, 0.1, 1.2, 2.4, 'glass');
  box(57, 0.03, 7, 13, 0.1, 5.5, 'road');
});

place('E_glass_tower', [-28, 13], plan(420, 565), 0.72);
place('B_curved_courtyard', [31, -10], plan(697, 547), 0.72);
place('C_long_teaching_wing', [-32, -36], plan(545, 630));
const buffer = await new GLTFExporter().parseAsync(root, { binary: true, onlyVisible: true });
const out = new URL('../public/models/', import.meta.url);
await mkdir(out, { recursive: true });
await writeFile(new URL('uit-campus.glb', out), Buffer.from(buffer));
console.log(`UIT reference GLB: ${(buffer.byteLength / 1024).toFixed(0)} KB`);
